namespace IMS.Application.DTO.Product
{
    public class PaginatedProductsDto
    {
        public List<ProductsDto> Products { get; set; } = new List<ProductsDto>();
        public int TotalCount { get; set; }
    }

}
