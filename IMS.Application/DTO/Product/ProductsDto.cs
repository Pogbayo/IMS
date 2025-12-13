namespace IMS.Application.DTO.Product
{
   public class ProductsDto
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public required string SKU { get; set; }
        public string? ImgUrl { get; set; }
        public decimal RetailPrice { get; set; } = 0m;
    }
}
