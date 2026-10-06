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
            var fileName = await MediaFilePolicy.ValidateAsync(file);

            using var stream = file.OpenReadStream();
            var isImage = !MediaFilePolicy.IsArchive(fileName)
                && file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
            UploadResult uploadResult;

            if (isImage)
            {
                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(fileName, stream),
                    Folder = "LetsLearn",
                    UploadPreset = _uploadPreset
                };
                uploadResult = await _cloudinary.UploadAsync(uploadParams);
            }
            else
            {
                var uploadParams = new RawUploadParams()
                {
                    File = new FileDescription(fileName, stream),
                    Folder = "LetsLearn",
                    // Raw assets need the extension in the public ID for downloads.
                    PublicId = $"{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}"
                };
                uploadResult = await _cloudinary.UploadAsync(uploadParams);
            }

            if (uploadResult.Error != null)
            {
                throw new Exception($"Cloudinary upload failed: {uploadResult.Error.Message}");
            }

            return new MediaUploadResponse
            {
                Name = fileName,
                DisplayUrl = uploadResult.SecureUrl.ToString(),
                DownloadUrl = uploadResult.SecureUrl.ToString()
            };
        }
    }
}
