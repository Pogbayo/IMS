using Microsoft.AspNetCore.Http;

namespace IMS.Application.DTO.User
{
    public class UpdateProfileImageRequest
    {
        public Guid UserId { get; set; }
        public IFormFile? File { get; set; }
    }
}
