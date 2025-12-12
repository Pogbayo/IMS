
namespace IMS.Domain.Entities
{
    public class Product : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string? ImgUrl { get; set; } 
        public decimal CostPrice { get; set; }
        public decimal RetailPrice { get; set; }
        public decimal Profit { get; set; }
        public Guid CategoryId { get; set; }
        public Category Category { get; set; } = default!;
        public Guid? SupplierId { get; set; }
        public Supplier Supplier { get; set; } = default!;
        public Guid CompanyId { get; set; }
        public Company Company { get; set; } = default!;
        public ICollection<ProductWarehouse> ProductWarehouses { get; set; } = new List<ProductWarehouse>();


        public decimal SetProfit()
        {
           var profit = RetailPrice - CostPrice ;
            return profit;
        }
    }
}
