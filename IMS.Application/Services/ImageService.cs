using CloudinaryDotNet;
using IMS.Application.Interfaces;
using IMS.Application.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace IMS.Application.Services
{
    public class ImageService : IImageService
    {
        private readonly Cloudinary _cloudinary;

        public ImageService(IOptions<CloudinarySettings> config)
        {
            var acc = new Account(config.Value.CloudName, config.Value.ApiKey, config.Value.ApiSecret);
            _cloudinary = new Cloudinary(acc);
        }
        public Task<string> UploadProductImageAsync(IFormFile file)
        {
            throw new NotImplementedException();
        }

        public Task<string> UploadProfileImageAsync(IFormFile file, string userId)
        {
            throw new NotImplementedException();
        }
    }
}
