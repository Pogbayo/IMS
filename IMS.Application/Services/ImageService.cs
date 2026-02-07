 using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
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
            //Console.WriteLine($"\n====== DEBUG: CloudName = {config.Value.CloudName} ======\n");
            var acc = new Account(config.Value.CloudName, config.Value.ApiKey, config.Value.ApiSecret);
            _cloudinary = new Cloudinary(acc);
        }

        public async Task<string> UploadImageAsync(IFormFile file, string folder , Guid EntityId)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file provided");

            if (!file.ContentType.StartsWith("image/"))
                throw new ArgumentException("Only image files are allowed");

            if (file.Length > 2_000_000) 
                throw new ArgumentException("Image size must not exceed 2MB");

            if (EntityId == Guid.Empty)
                throw new ArgumentException("Record ID can not be null");

            await using var stream = file.OpenReadStream();

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = folder, 
                PublicId = EntityId.ToString(), 
                Overwrite = true,
                Transformation = new Transformation()
                    .Quality("auto")
                    .FetchFormat("auto")
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception("Image upload failed");

            return uploadResult.SecureUrl.ToString();
        }

    }
}
