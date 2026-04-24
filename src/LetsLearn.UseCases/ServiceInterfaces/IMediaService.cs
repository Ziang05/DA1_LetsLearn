using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface IMediaService
    {
        Task<MediaUploadResponse> UploadFileAsync(IFormFile file);
    }

    public class MediaUploadResponse
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayUrl { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
    }
}
