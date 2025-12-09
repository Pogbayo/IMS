namespace IMS.Application.DTO
{
    public class WarehouseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public Guid CompanyId { get; set; }
    }
}