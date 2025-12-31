using IMS.Application.DTO.Warehouse;
using System.ComponentModel.DataAnnotations;

namespace IMS.Application.DTO.Company
{ 
    public class CompanyCreateDto
    {
        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;
        public string? AdminPhoneNumber { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public string? CompanyName { get; set; }
        public string? CompanyEmail { get; set; }
        public string CompanyLine { get; set; } = string.Empty;
        public string? HeadOffice { get; set; }

        //public IList<CreateWarehouseDto> Warehouses = new List<CreateWarehouseDto>();
    } 
}