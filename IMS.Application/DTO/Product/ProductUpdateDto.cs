namespace IMS.Application.DTO.Product
{
    public class ProductUpdateDto
    {
        public required string Name { get; set; }
        public required string SKU { get; set; }
        public required string ImgUrl { get; set; }
        public decimal Price { get; set; }
    }
}