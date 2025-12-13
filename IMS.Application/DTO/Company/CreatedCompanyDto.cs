using IMS.Application.DTO.Product;
namespace IMS.Application.DTO.Company
{
    public class CreatedCompanyDto
    {
        public Guid AdminId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public Guid CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string HeadOffice { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int TotalPurchases { get; set; }
        public decimal TotalInventoryValue { get; set; }
        public decimal SalesTrend { get; set; } //How much we made (this moonth)
        public List<ProductsDto> TopProductsBySales { get; set; } = new List<ProductsDto>();
        public int TotalSalesPerMonth { get; set; }
        public List<ProductsDto> LowOnStockProducts { get; set; } = new List<ProductsDto>();
    }
}
