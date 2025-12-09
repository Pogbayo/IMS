namespace IMS.Application.DTO.User
{
    public class UpdateUserDto
    {
        public string? FirstName { get; set; } 
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public bool? IsCompanyAdmin { get; set; }  
    }
}
