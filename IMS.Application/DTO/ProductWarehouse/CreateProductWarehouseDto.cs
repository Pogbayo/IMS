namespace IMS.Application.DTO.ProductWarehouse
{
    public class CreateProductWarehouseDto
    {
        public Guid ProductId { get; set; }
        public Guid WarehouseId { get; set; }
        public int Quantity { get; set; }
    }
}
