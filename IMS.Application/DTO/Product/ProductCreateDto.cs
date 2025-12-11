namespace IMS.Application.DTO.Product
{
    public class ProductCreateDto
    {
        public required string Name { get; set; }
        public string? ImgUrl { get; set; }
        public decimal Price { get; set; }
        public List<string> Category { get; set; } = new List<string>();
        public Guid CompanyId { get; set; }
        public Guid WarehouseId { get; set; }
        public int Quantity { get; set; }
        public Guid SupplierId { get; set; }
    }
}