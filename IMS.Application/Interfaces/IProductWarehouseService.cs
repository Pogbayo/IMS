using IMS.Application.DTO.ProductWarehouse;
using IMS.Domain.Enums;
namespace IMS.Application.Interfaces
{
    public interface IProductWarehouseService
    {
        Task<List<ProductWarehouseDto>> GetProductsInWarehouse(Guid warehouseId);
        Task UpdateProductInWarehouseAsync(Guid productId, Guid warehouseId, int quantityChanged , Guid userId, ProductUpdateInWarehouseType UpdateType);
        Task UpdateProductStockAsync(Guid productWarehouseId, int newQuantity, Guid userId);
        Task TransferProductAsync(
            Guid productId,
            Guid fromWarehouseId,
            Guid toWarehouseId,
            int quantity,
            Guid userId);
        Task ReduceStockAsync(Guid productWarehouseId, int quantitySold, Guid userId); //There will a part on the UI where the staff can enter what they sold to client and send it to the backend and I create a stock transaction for it
        Task<int> GetProductCountAsync(Guid productId, Guid warehouseId);
        //public Task AddStockAsync(Guid productId, Guid warehouseId, int quantity, Guid userId, string? note)
        //{
        //    throw new NotImplementedException();
        //}

        public Task AdjustStockAsync(Guid productId, Guid warehouseId, int quantity, Guid userId, string? note)
        {
            throw new NotImplementedException();
        }

        public Task TransferStockAsync(Guid productId, Guid fromWarehouseId, Guid toWarehouseId, int quantity, Guid userId, string? note)
        {
            throw new NotImplementedException();
        }
    }
}
