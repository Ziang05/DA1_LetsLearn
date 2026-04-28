using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;

namespace LetsLearn.UseCases.Services
{
    public class MediaService : IMediaService
    {
        private readonly Cloudinary _cloudinary;
        private readonly string _uploadPreset;

        public MediaService(IConfiguration configuration)
        {
            var cloudName = configuration["Cloudinary:CloudName"];
            var apiKey = configuration["Cloudinary:ApiKey"];
            var apiSecret = configuration["Cloudinary:ApiSecret"];
            _uploadPreset = configuration["Cloudinary:UploadPreset"];

            if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
            {
                throw new ArgumentException("Cloudinary settings are missing in configuration");
            }

            Account account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account);
        }

        public async Task<MediaUploadResponse> UploadFileAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty");

            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "LetsLearn",
                UploadPreset = _uploadPreset
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                throw new Exception($"Cloudinary upload failed: {uploadResult.Error.Message}");
            }

            return new MediaUploadResponse
            {
                Name = file.FileName,
                DisplayUrl = uploadResult.SecureUrl.ToString(),
                DownloadUrl = uploadResult.SecureUrl.ToString()
            };
        }
    }
}
