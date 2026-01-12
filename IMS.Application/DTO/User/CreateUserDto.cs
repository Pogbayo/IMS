using IMS.Domain.Enums;

namespace IMS.Application.DTO.User
{
    public class CreateUserDto
    {
        public Guid CompanyId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public Roles UserRole { get; set; }
        public string Email { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
    }
}
