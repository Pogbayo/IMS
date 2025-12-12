using IMS.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Domain.Entities
{
    public class StockTransaction : BaseEntity
    {
        [ForeignKey(nameof(ProductWarehouse))]
        public Guid ProductWarehouseId { get; set; }
        public ProductWarehouse? ProductWarehouse { get; set; } = default!;
        public int QuantityChanged { get; set; }
        public TransactionType Type { get; set; } = default!; 
        public string? Note { get; set; }
        public Guid UserId { get; set; } 
        public AppUser User { get; set; } = default!;
        public Guid CompanyId { get; set; }
        public virtual Warehouse? FromWarehouse { get; set; }
        public Guid? FromWarehouseId { get; set; }
        public virtual Warehouse? ToWarehouse { get; set; }
        public Guid? ToWarehouseId { get; set; }
        public Company Company { get; set; } = default!;
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow; 
    }
}
