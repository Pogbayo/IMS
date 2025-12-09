namespace IMS.Application.DTO.User
{
    public class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty; 
        public UserDto User { get; set; } = default!;
    }
}
