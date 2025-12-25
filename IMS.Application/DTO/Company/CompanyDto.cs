using IMS.Application.DTO.Product;
namespace IMS.Application.DTO.Company
{
    public class CompanyDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public string CompanyEmail { get; set; } = string.Empty;
        public string HeadOffice { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public decimal TotalInventoryValue { get; set; }
        public decimal TotalPurchases { get; set; }
        public decimal SalesTrend { get; set; } //How much we made (this moonth)
        public List<TopProductDto> TopProductsBySales { get; set; } = new List<TopProductDto>();
        public decimal TotalSalesPerMonth { get; set; }
        public List<LowOnStockProduct> LowOnStockProducts { get; set; } = new List<LowOnStockProduct>();
        public int TotalNumberOfProducts { get; set; }
    }
}
