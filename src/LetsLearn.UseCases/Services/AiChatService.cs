using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LetsLearn.UseCases.Services
{
    public class AiChatService : IAiChatService
    {
        private readonly IUnitOfWork _uow;
        private readonly IAiQuestionGenerationService _aiQuestionGenService;
        private readonly IAiLlmClient _llmClient;
        private readonly ILogger<AiChatService> _logger;

        public AiChatService(
            IUnitOfWork uow,
            IAiQuestionGenerationService aiQuestionGenService,
            IAiLlmClient llmClient,
            ILogger<AiChatService> logger)
        {
            _uow = uow;
            _aiQuestionGenService = aiQuestionGenService;
            _llmClient = llmClient;
            _logger = logger;
        }

        public async Task<string> SummarizeChatAsync(Guid conversationId, int limit, CancellationToken ct = default)
        {
            _logger.LogInformation("[AI Chat] Summarizing chat for conversation: {ConversationId}, limit: {Limit}", conversationId, limit);

            var messages = await _uow.Messages.GetMessagesByConversationIdAsync(conversationId);
            var targetMessages = messages
                .Where(m => m != null)
                .OrderBy(m => m.Timestamp)
                .TakeLast(limit)
                .ToList();

            if (targetMessages.Count < 2)
            {
                return "Không có đủ tin nhắn trong hội thoại để thực hiện tóm tắt (cần ít nhất 2 tin nhắn).";
            }

            var senderIds = targetMessages.Select(m => m.SenderId).Distinct().ToList();
            var senders = await _uow.Users.FindAsync(u => senderIds.Contains(u.Id), ct);
            var senderMap = senders
                .Where(u => u != null)
                .ToDictionary(u => u.Id, u => u.Username ?? u.Email);

            var chatTranscript = string.Join("\n", targetMessages.Select(m =>
            {
                var senderName = senderMap.TryGetValue(m.SenderId, out var name) ? name : "Unknown";
                return $"{senderName}: {m.Content}";
            }));

            var systemPrompt = @"Bạn là một trợ lý AI học tập thông minh thuộc ứng dụng Let'sLearn. Nhiệm vụ của bạn là đọc lịch sử cuộc trò chuyện chat của nhóm sinh viên và tạo ra một bản tóm tắt ngắn gọn, súc tích bằng tiếng Việt.
Bản tóm tắt phải được trình bày dưới định dạng Markdown đẹp mắt, phân tách rõ ràng thành các phần:
- 📌 **Các chủ đề thảo luận chính**
- 🤝 **Các quyết định hoặc đồng thuận đạt được** (nếu có)
- ❓ **Các câu hỏi chưa được giải quyết** (nếu có)
- 🚀 **Các hành động tiếp theo cần thực hiện** (nếu có, phân vai rõ ràng cho từng thành viên nếu có tên)

Yêu cầu định dạng đầu ra bắt buộc là JSON thô có cấu trúc sau:
{
  ""summary"": ""chuỗi markdown tóm tắt chi tiết ở đây""
}";

            var userPrompt = $"Dưới đây là lịch sử cuộc thảo luận gần đây của nhóm:\n\n{chatTranscript}\n\nHãy tóm tắt cuộc thảo luận này theo đúng định dạng JSON yêu cầu.";

            var jsonResult = await _llmClient.GenerateJsonAsync(systemPrompt, userPrompt, 0.5m, ct);
            if (string.IsNullOrWhiteSpace(jsonResult))
            {
                throw new InvalidOperationException("Không nhận được phản hồi tóm tắt từ mô hình AI.");
            }

            try
            {
                using var doc = JsonDocument.Parse(jsonResult);
                if (doc.RootElement.TryGetProperty("summary", out var summaryProp))
                {
                    return summaryProp.GetString() ?? "Không thể trích xuất nội dung tóm tắt.";
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "[AI Chat] Failed to parse summary JSON. Raw result: {Raw}", jsonResult);
            }

            return jsonResult; // Trả về thô nếu không parse được JSON
        }

        public async Task<Guid> GenerateQuizFromSharedDocumentAsync(Guid messageId, string bloomLevel, int questionCount, Guid userId, CancellationToken ct = default)
        {
            _logger.LogInformation("[AI Chat] Generating quiz from shared document in message {MessageId} by user {UserId}", messageId, userId);

            var message = await _uow.Messages.GetByIdAsync(messageId, ct)
                ?? throw new KeyNotFoundException("Không tìm thấy tin nhắn được chia sẻ.");

            if (string.IsNullOrWhiteSpace(message.FileUrl) || string.IsNullOrWhiteSpace(message.FileName))
            {
                throw new ArgumentException("Tin nhắn được chọn không chứa tệp tin tài liệu hợp lệ.");
            }

            var extension = Path.GetExtension(message.FileName).ToLowerInvariant();
            if (extension is not ".docx" and not ".pdf" and not ".txt")
            {
                throw new NotSupportedException("Chỉ hỗ trợ sinh câu hỏi từ các tài liệu có định dạng .docx, .pdf hoặc .txt.");
            }

            var courseId = message.ConversationId.ToString();
            var course = await _uow.Course.GetByIdAsync(courseId, ct)
                ?? throw new KeyNotFoundException("Cuộc hội thoại chat này không thuộc về một khóa học hợp lệ.");

            // 1. Tải tài liệu về dạng byte array
            byte[] fileBytes;
            if (message.FileUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                fileBytes = await httpClient.GetByteArrayAsync(message.FileUrl, ct);
            }
            else
            {
                var localPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", message.FileUrl.TrimStart('/'));
                if (!File.Exists(localPath))
                {
                    throw new FileNotFoundException($"Không tìm thấy file tài liệu trên máy chủ: {message.FileName}");
                }
                fileBytes = await File.ReadAllBytesAsync(localPath, ct);
            }

            // 2. Tạo IFormFile mô phỏng
            var contentType = GetContentType(message.FileName);
            var formFile = new InMemoryFormFile(fileBytes, message.FileName, contentType);

            // 3. Upload tài liệu và chunking (bypassTeacherCheck = true)
            var uploadRes = await _aiQuestionGenService.UploadDocumentAsync(formFile, courseId, userId, bypassTeacherCheck: true, ct);

            // 4. Kích hoạt sinh câu hỏi (bypassTeacherCheck = true)
            var genReq = new GenerateAiQuestionsRequest
            {
                DocumentId = uploadRes.DocumentId,
                CourseId = courseId,
                BloomLevel = bloomLevel,
                QuestionType = "MultipleChoice",
                QuestionCount = questionCount,
                TopK = 6
            };
            var genRes = await _aiQuestionGenService.GenerateQuestionsAsync(genReq, userId, bypassTeacherCheck: true, ct);

            // 5. Chạy trực tiếp đồng bộ tiến trình Dual-LLM sinh và đánh giá
            await _aiQuestionGenService.ProcessGenerationJobAsync(genRes.JobId, ct);

            // 6. Lấy các câu hỏi đã sinh thành công
            var generatedQuestions = (await _uow.AiGeneratedQuestions.FindAsync(q => q.JobId == genRes.JobId, ct))
                .Where(q => q != null)
                .ToList();

            // Lọc câu hỏi đạt chất lượng (Passed). Nếu không có, lấy top N câu có điểm cao nhất.
            var passedQuestions = generatedQuestions.Where(q => q.Status == "passed").ToList();
            if (passedQuestions.Count == 0)
            {
                passedQuestions = generatedQuestions
                    .OrderByDescending(q => q.Score)
                    .Take(questionCount)
                    .ToList();
            }

            if (passedQuestions.Count == 0)
            {
                throw new InvalidOperationException("Mô hình AI không sinh được câu hỏi nào đạt chất lượng từ tài liệu này.");
            }

            // 7. Xác định hoặc tạo mới Section để lưu Topic
            var sections = (await _uow.Sections.FindAsync(s => s.CourseId == courseId, ct))
                .Where(s => s != null)
                .ToList();
            var targetSection = sections.FirstOrDefault();
            if (targetSection == null)
            {
                targetSection = new Section
                {
                    Id = Guid.NewGuid(),
                    Title = "Nội dung chung",
                    CourseId = courseId
                };
                await _uow.Sections.AddAsync(targetSection);
            }

            // 8. Tạo Topic dạng quiz
            var topic = new Topic
            {
                Id = Guid.NewGuid(),
                Title = $"Quiz tự động - {Path.GetFileNameWithoutExtension(message.FileName)} ({DateTime.Now:dd/MM/yyyy})",
                Type = "quiz",
                SectionId = targetSection.Id
            };

            var quiz = new TopicQuiz
            {
                TopicId = topic.Id,
                Description = $"Bài trắc nghiệm ôn tập nhanh được sinh tự động bởi AI từ tài liệu học tập: {message.FileName}.",
                Open = DateTime.UtcNow,
                GradingMethod = "Highest Grade",
                AttemptAllowed = "3", // Attempt allowed được định nghĩa dạng string? trong thực thể
                Questions = new List<TopicQuizQuestion>()
            };

            // 9. Map câu hỏi sang TopicQuizQuestion
            foreach (var genQ in passedQuestions)
            {
                var questionId = Guid.NewGuid();
                var choices = JsonSerializer.Deserialize<List<AiGeneratedChoiceDto>>(genQ.ChoicesJson ?? "[]")
                    ?? new List<AiGeneratedChoiceDto>();

                var quizQuestion = new TopicQuizQuestion
                {
                    Id = questionId,
                    TopicQuizId = topic.Id,
                    QuestionName = genQ.QuestionName,
                    QuestionText = genQ.QuestionText,
                    Type = "Choices Answer", // Dạng trắc nghiệm
                    DefaultMark = 1,
                    Multiple = genQ.Type == "MultipleChoice",
                    Choices = choices.Select(c => new TopicQuizQuestionChoice
                    {
                        Id = Guid.NewGuid(),
                        QuizQuestionId = questionId,
                        Text = c.Text,
                        GradePercent = c.GradePercent,
                        Feedback = c.Feedback
                    }).ToList()
                };

                quiz.Questions.Add(quizQuestion);
            }

            await _uow.Topics.AddAsync(topic);
            await _uow.TopicQuizzes.AddAsync(quiz);

            // 10. Gửi tin nhắn bot thông báo kết quả vào nhóm
            var botMessage = new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = message.ConversationId,
                SenderId = userId, // Sử dụng userId hợp lệ để thỏa mãn FK constraint
                Content = $"🤖 **AI Bot thông báo**: Tôi đã tạo thành công bộ Quiz ôn tập **\"{topic.Title}\"** gồm {quiz.Questions.Count} câu hỏi trắc nghiệm tự động từ tài liệu **\"{message.FileName}\"**. Mọi người hãy vào phần ôn tập khóa học để bắt đầu làm bài nhé!",
                Timestamp = DateTime.UtcNow
            };
            await _uow.Messages.AddAsync(botMessage);

            await _uow.CommitAsync();

            _logger.LogInformation("[AI Chat] Quiz created successfully. TopicId={TopicId}, QuestionCount={Count}", topic.Id, quiz.Questions.Count);
            return topic.Id;
        }

        private string GetContentType(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
        }
    }

    public class InMemoryFormFile : IFormFile
    {
        private readonly byte[] _fileBytes;

        public InMemoryFormFile(byte[] fileBytes, string fileName, string contentType)
        {
            _fileBytes = fileBytes;
            FileName = fileName;
            ContentType = contentType;
            Length = fileBytes.Length;
            Name = "file";
        }

        public string ContentType { get; }
        public string ContentDisposition => $"form-data; name=\"{Name}\"; filename=\"{FileName}\"";
        public IHeaderDictionary Headers => new HeaderDictionary();
        public long Length { get; }
        public string Name { get; }
        public string FileName { get; }

        public Stream OpenReadStream()
        {
            return new MemoryStream(_fileBytes);
        }

        public void CopyTo(Stream target)
        {
            using var stream = OpenReadStream();
            stream.CopyTo(target);
        }

        public async Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            using var stream = OpenReadStream();
            await stream.CopyToAsync(target, cancellationToken);
        }
    }
}
