
namespace IMS.Application.DTO.ProductWarehouse
{
    public class ProductWarehouseDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public Guid WarehouseId { get; set; }
        public int Quantity { get; set; }
    }
}
