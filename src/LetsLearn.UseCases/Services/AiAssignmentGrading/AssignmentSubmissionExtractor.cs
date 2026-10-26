using System.Net;
using System.Text;
using LetsLearn.Core.Entities;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace LetsLearn.UseCases.Services.AiAssignmentGrading
{
    public sealed class AssignmentSubmissionExtractor : IAssignmentSubmissionExtractor
    {
        private const string HttpClientName = "AssignmentSubmissionStorage";
        private const int DefaultMaxUploadFileSizeMb = 25;
        private const int DefaultMaxExtractedTextLength = 100_000;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IDocumentTextExtractor _documentTextExtractor;
        private readonly string _cloudName;
        private readonly int _maxFileBytes;
        private readonly int _maxTextLength;

        public AssignmentSubmissionExtractor(
            IHttpClientFactory httpClientFactory,
            IDocumentTextExtractor documentTextExtractor,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _documentTextExtractor = documentTextExtractor;
            _cloudName = configuration["Cloudinary:CloudName"] ?? string.Empty;
            _maxFileBytes = checked(GetPositiveSetting(configuration, "AIQuota:MaxUploadFileSizeMb", DefaultMaxUploadFileSizeMb) * 1024 * 1024);
            _maxTextLength = GetPositiveSetting(configuration, "AIGrading:MaxExtractedTextLength", DefaultMaxExtractedTextLength);
        }

        public async Task<AssignmentSubmissionSnapshot> ExtractAsync(
            AssignmentResponse submission,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(submission);
            if (submission.Files == null || submission.Files.Count == 0)
            {
                throw new AssignmentSubmissionExtractionException("submission_file_missing", "Bài nộp không có tệp để trích xuất.");
            }

            var extractedTexts = new List<string>();
            foreach (var storedFile in submission.Files.OrderBy(file => file.Id))
            {
                var extension = Path.GetExtension(storedFile.Name ?? string.Empty).ToLowerInvariant();
                if (extension is not (".pdf" or ".docx" or ".txt"))
                {
                    throw new AssignmentSubmissionExtractionException("unsupported_file_type", "Chỉ hỗ trợ PDF có văn bản, DOCX và TXT.");
                }

                var uri = ValidateStoredUrl(storedFile.DownloadUrl, extension);
                extractedTexts.Add(await DownloadAndExtractAsync(uri, storedFile.Name!, ct));
            }

            var normalized = Normalize(string.Join("\n\n", extractedTexts));
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new AssignmentSubmissionExtractionException("empty_extracted_text", "Không tìm thấy văn bản trong tệp bài nộp.");
            }
            if (normalized.Length > _maxTextLength)
            {
                throw new AssignmentSubmissionExtractionException("extracted_text_too_long", $"Văn bản sau trích xuất vượt giới hạn {_maxTextLength} ký tự.");
            }

            return new AssignmentSubmissionSnapshot
            {
                Text = normalized,
                ContentHash = AssignmentGradingInputHasher.ComputeSubmissionHash(normalized),
                FileCount = extractedTexts.Count
            };
        }

        private async Task<string> DownloadAndExtractAsync(Uri uri, string fileName, CancellationToken ct)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            var client = _httpClientFactory.CreateClient(HttpClientName);
            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new AssignmentSubmissionExtractionException("file_processing_timeout", "Tải hoặc trích xuất tệp vượt quá thời gian cho phép.");
            }
            catch (HttpRequestException ex)
            {
                throw new AssignmentSubmissionExtractionException("stored_file_unavailable", "Không thể tải tệp bài nộp từ kho lưu trữ đã đăng ký.", ex);
            }
            using (response)
            {
                return await ExtractDownloadedResponseAsync(response, fileName, ct, timeout.Token);
            }
        }

        private async Task<string> ExtractDownloadedResponseAsync(
            HttpResponseMessage response,
            string fileName,
            CancellationToken ct,
            CancellationToken timeoutToken)
        {
            if (response.StatusCode is >= HttpStatusCode.MovedPermanently and <= HttpStatusCode.PermanentRedirect)
            {
                throw new AssignmentSubmissionExtractionException("storage_redirect_rejected", "Nguồn lưu trữ trả về redirect; không tải theo URL chuyển hướng.");
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new AssignmentSubmissionExtractionException("stored_file_unavailable", "Không thể tải tệp bài nộp từ kho lưu trữ đã đăng ký.");
            }
            if (response.Content.Headers.ContentLength is long length && length > _maxFileBytes)
            {
                throw new AssignmentSubmissionExtractionException("file_too_large", "Tệp bài nộp vượt giới hạn kích thước cho phép.");
            }

            var tempPath = Path.Combine(Path.GetTempPath(), $"letslearn-grading-{Guid.NewGuid():N}{Path.GetExtension(fileName)}");
            try
            {
                await using (var source = await response.Content.ReadAsStreamAsync(timeoutToken))
                await using (var target = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    var buffer = new byte[81920];
                    var total = 0L;
                    int read;
                    while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), timeoutToken)) > 0)
                    {
                        total += read;
                        if (total > _maxFileBytes)
                        {
                            throw new AssignmentSubmissionExtractionException("file_too_large", "Tệp bài nộp vượt giới hạn kích thước cho phép.");
                        }
                        await target.WriteAsync(buffer.AsMemory(0, read), timeoutToken);
                    }
                    if (total == 0)
                    {
                        throw new AssignmentSubmissionExtractionException("empty_file", "Tệp bài nộp rỗng.");
                    }
                }

                await ValidateFileSignatureAsync(tempPath, Path.GetExtension(fileName).ToLowerInvariant(), timeoutToken);
                var extracted = await _documentTextExtractor.ExtractAsync(fileName, tempPath, timeoutToken);
                var normalized = Normalize(extracted.Text);
                if (string.IsNullOrWhiteSpace(normalized) && Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    throw new AssignmentSubmissionExtractionException("pdf_has_no_text", "PDF không có lớp văn bản có thể đọc; PDF scan cần được chấm thủ công.");
                }
                return normalized;
            }
            catch (AssignmentSubmissionExtractionException)
            {
                throw;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new AssignmentSubmissionExtractionException("file_processing_timeout", "Tải hoặc trích xuất tệp vượt quá thời gian cho phép.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new AssignmentSubmissionExtractionException("file_extraction_failed", "Tệp bài nộp bị hỏng hoặc không thể trích xuất.", ex);
            }
            finally
            {
                try { File.Delete(tempPath); }
                catch (IOException) { /* Best effort cleanup; path is generated locally. */ }
                catch (UnauthorizedAccessException) { /* Best effort cleanup; path is generated locally. */ }
            }
        }

        private Uri ValidateStoredUrl(string? rawUrl, string expectedExtension)
        {
            if (string.IsNullOrWhiteSpace(_cloudName)
                || !Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || !string.Equals(uri.Host, "res.cloudinary.com", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrEmpty(uri.UserInfo)
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment))
            {
                throw new AssignmentSubmissionExtractionException("invalid_storage_reference", "Tệp không trỏ tới kho lưu trữ Cloudinary được cấu hình.");
            }

            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.UnescapeDataString).ToArray();
            if (segments.Length < 6
                || !string.Equals(segments[0], _cloudName, StringComparison.Ordinal)
                || !string.Equals(segments[1], "raw", StringComparison.Ordinal)
                || !string.Equals(segments[2], "upload", StringComparison.Ordinal)
                || !segments.Skip(4).Any(segment => string.Equals(segment, "LetsLearn", StringComparison.Ordinal))
                || segments.Any(segment => segment is "." or ".." || segment.Contains('/') || segment.Contains('\\'))
                || !string.Equals(Path.GetExtension(segments[^1]), expectedExtension, StringComparison.OrdinalIgnoreCase))
            {
                throw new AssignmentSubmissionExtractionException("invalid_storage_reference", "Đường dẫn tệp không khớp thư mục lưu trữ bài học đã cấp.");
            }

            return uri;
        }

        private static async Task ValidateFileSignatureAsync(string path, string extension, CancellationToken ct)
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16, useAsync: true);
            var header = new byte[8];
            var read = await stream.ReadAsync(header.AsMemory(), ct);
            var valid = extension switch
            {
                ".pdf" => read >= 5 && header.Take(5).SequenceEqual(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2d }),
                ".docx" => read >= 4 && header[0] == 0x50 && header[1] == 0x4b
                    && ((header[2] == 0x03 && header[3] == 0x04)
                        || (header[2] == 0x05 && header[3] == 0x06)
                        || (header[2] == 0x07 && header[3] == 0x08)),
                ".txt" => read > 0 && !header.Take(read).Contains((byte)0),
                _ => false
            };

            if (!valid)
            {
                throw new AssignmentSubmissionExtractionException("file_type_mismatch", "Nội dung tệp không khớp với phần mở rộng PDF, DOCX hoặc TXT.");
            }
        }

        private static string Normalize(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var normalized = text.Normalize(NormalizationForm.FormC)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            var lines = normalized.Split('\n').Select(line => string.Join(' ', line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
            return string.Join('\n', lines).Trim();
        }

        private static int GetPositiveSetting(IConfiguration configuration, string key, int defaultValue) =>
            int.TryParse(configuration[key], out var value) && value > 0 ? value : defaultValue;
    }
}
