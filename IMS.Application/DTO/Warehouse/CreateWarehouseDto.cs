namespace IMS.Application.DTO.Warehouse
{
    public class CreateWarehouseDto
    {
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public Guid CompanyId { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
    }
}