
namespace IMS.Domain.Entities
{
    public class CompanyDailyStat : BaseEntity
    {
        public Guid CompanyId { get; set; }
        public Company Company { get; set; } = null!;
        public decimal TotalInventoryValue { get; set; }
        public string TopProductsBySalesJson { get; set; } = null!;
        public string LowOnStockProductsJson { get; set; } = null!;
        public DateTime StatDate { get; set; } = DateTime.UtcNow.Date;
    }
}
