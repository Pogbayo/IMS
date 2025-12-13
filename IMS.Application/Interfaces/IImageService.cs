using Microsoft.AspNetCore.Http;

namespace IMS.Application.Interfaces
{
    public interface IImageService
    {
        Task<string> UploadImageAsync(IFormFile file, string folder, Guid EntityId);
    }
}
