using IMS.Application.DTO.Product;
using IMS.Application.DTO.StockTransaction;
using IMS.Domain.Enums;


namespace IMS.Application.Interfaces
{
    public interface IProductHelper
    {
        Task<List<WarehouseCount>> GetProductWarehouseQuantitiesAsync(IQueryable<Guid> productId);
        Task<int> GetOverallProductCountAsync(IQueryable<Guid> productId);
        Task<ProductStockLevel> GetProductStockLevelAsync(IQueryable<Guid> productId);
        Task<SupplierInfo> GetSupplierInfoAsync(IQueryable<Guid> supplierId);
        Task<List<StockTransactionDto>> GetProductTransactionsAsync(IQueryable<Guid> productId);
    }
}
