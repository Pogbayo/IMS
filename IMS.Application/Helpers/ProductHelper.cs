using IMS.Application.DTO.Product;
using IMS.Application.DTO.StockTransaction;
using IMS.Application.Interfaces;
using IMS.Domain.Enums;

namespace IMS.Application.Helpers
{
    public class ProductHelper : IProductHelper
    {
        public Task<int> GetOverallProductCountAsync(IQueryable<Guid> productId)
        {
            throw new NotImplementedException();
        }

        public Task<ProductStockLevel> GetProductStockLevelAsync(IQueryable<Guid> productId)
        {
            throw new NotImplementedException();
        }

        public Task<List<StockTransactionDto>> GetProductTransactionsAsync(IQueryable<Guid> productId)
        {
            throw new NotImplementedException();
        }

        public Task<List<WarehouseCount>> GetProductWarehouseQuantitiesAsync(IQueryable<Guid> productId)
        {
            throw new NotImplementedException();
        }

        public Task<SupplierInfo> GetSupplierInfoAsync(IQueryable<Guid> supplierId)
        {
            throw new NotImplementedException();
        }
    }
}
