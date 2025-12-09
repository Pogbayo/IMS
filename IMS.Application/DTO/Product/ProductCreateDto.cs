namespace IMS.Application.DTO.Product
{
    public class ProductCreateDto
    {
        public required string Name { get; set; }
        public required string SKU { get; set; }
        public string? ImgUrl { get; set; }
        public decimal Price { get; set; }
        public Guid CategoryId { get; set; }
        public Guid SupplierId { get; set; }
    }
}