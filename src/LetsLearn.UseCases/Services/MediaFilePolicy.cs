using Microsoft.AspNetCore.Http;

namespace LetsLearn.UseCases.Services;

public static class MediaFilePolicy
{
    public const long MaxFileSize = 50 * 1024 * 1024;
    // Leave room for multipart headers in the HTTP request.
    public const long MaxRequestSize = MaxFileSize + 1024 * 1024;

    public static bool IsArchive(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() is ".zip" or ".rar" or ".7z";

    public static async Task<string> ValidateAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File không được để trống.");
        if (file.Length > MaxFileSize)
            throw new ArgumentException("Dung lượng tối đa là 50 MB mỗi file.");

        var name = Path.GetFileName(file.FileName.Replace('\\', '/')).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name.Any(char.IsControl))
            throw new ArgumentException("Tên file không hợp lệ (tối đa 255 ký tự).");

        if (!IsArchive(name)) return name;

        // Check the container signature, not the browser-supplied MIME type.
        // Archives are stored as opaque files and are never extracted on the server.
        using var stream = file.OpenReadStream();
        var header = new byte[8];
        var read = 0;
        while (read < header.Length)
        {
            var count = await stream.ReadAsync(header.AsMemory(read));
            if (count == 0) break;
            read += count;
        }

        bool StartsWith(params byte[] signature) =>
            read >= signature.Length && header.Take(signature.Length).SequenceEqual(signature);

        var valid = Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".zip" => StartsWith(0x50, 0x4b, 0x03, 0x04) || StartsWith(0x50, 0x4b, 0x05, 0x06) || StartsWith(0x50, 0x4b, 0x07, 0x08),
            ".rar" => StartsWith(0x52, 0x61, 0x72, 0x21, 0x1a, 0x07, 0x00) || StartsWith(0x52, 0x61, 0x72, 0x21, 0x1a, 0x07, 0x01, 0x00),
            ".7z" => StartsWith(0x37, 0x7a, 0xbc, 0xaf, 0x27, 0x1c),
            _ => false
        };
        if (!valid)
            throw new ArgumentException("Nội dung file không khớp định dạng ZIP, RAR hoặc 7z. Vui lòng tạo lại file nén.");

        return name;
    }
}
