
namespace IMS.Application.DTO.User
{
    public class AddedUserResponseDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string LoginPassword { get; set; } = string.Empty;
    }
}
