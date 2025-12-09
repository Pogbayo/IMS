using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace IMS.Application.Interfaces
{
    public interface IImageService
    {
        Task<string> UploadProductImageAsync(IFormFile file);
        Task<string> UploadProfileImageAsync(IFormFile file, string userId);  
    }
}
