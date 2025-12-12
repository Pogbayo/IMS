using IMS.Application.DTO.StockTransaction;
using IMS.Domain.Entities; 
using IMS.Domain.Enums;

namespace IMS.Application.DTO.Product
{
    public class WarehouseCount
    {
        public string? WarehouseName { get; set; }
        public int Quantity { get; set; }
    }

    public class SupplierInfo
    {
        public string? SupplierName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
    }

    public class ProductDto
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public required string SKU { get; set; }
        public string? ImgUrl { get; set; }
        public decimal Price { get; set; } = 0m;
        public List<WarehouseCount> Quantity { get; set; } = default!;
        public int OverAllCount { get; set; }
        public ProductStockLevel StockLevel { get; set; }
        public SupplierInfo SupplierInfo { get; set; } = default!;
        public List<StockTransactionDto> Transactions { get; set; } = new List<StockTransactionDto>();
    }
}