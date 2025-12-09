
namespace IMS.Domain.Entities
{
    public class ProductWarehouse : BaseEntity
    {
        public Guid ProductId { get; set; }
        public Product? Product { get; set; }
        public Guid WarehouseId { get; set; }
        public Warehouse? Warehouse { get; set; }
        public int Quantity { get; set; }
        public bool IsActive { get; set; } = false;
        public ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();
    }
}
