namespace IMS.Application.DTO.User
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public required string Email { get; set; }
        public  string ?FirstName { get; set; }
        public  string? LastName { get; set; }
        public  string? UserName { get; set; }
        public Guid? CompanyId { get; set; }
        public bool IsCompanyAdmin { get; set; } = false;
        public string? PhoneNumber { get; set; }
    }
}