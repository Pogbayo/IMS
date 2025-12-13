using IMS.Application.DTO.Product;
namespace IMS.Application.DTO.Warehouse
{
    public class WarehouseProductsResponse
    {
        public int OverAllCount { get; set; }
        public List<ProductsDto> Products { get; set; } = new();
    }

}
