using IMS.Application.Interfaces;
using Microsoft.AspNetCore.Http;
namespace IMS.Application.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid GetCurrentUserId()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("id");
            if (userIdClaim == null)
            {
                throw new UnauthorizedAccessException("User ID not found");
            }
            return Guid.Parse(userIdClaim.Value);
        }
    }
}
