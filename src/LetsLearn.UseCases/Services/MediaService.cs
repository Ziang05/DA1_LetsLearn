using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.Services
{
    public class MediaService : IMediaService
    {
        private readonly string _uploadFolder;
        private readonly string _baseUrl = "http://localhost:5169";

        public MediaService(string uploadPath)
        {
            _uploadFolder = uploadPath;
            Console.WriteLine($"[MediaService] Upload folder: {_uploadFolder}");
            
            if (!Directory.Exists(_uploadFolder))
            {
                Directory.CreateDirectory(_uploadFolder);
            }
        }

        public async Task<MediaUploadResponse> UploadFileAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty");

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(_uploadFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var relativeUrl = $"/uploads/{fileName}";
            var fullUrl = $"{_baseUrl}{relativeUrl}";

            return new MediaUploadResponse
            {
                Name = file.FileName,
                DisplayUrl = fullUrl,
                DownloadUrl = fullUrl
            };
        }
    }
}
