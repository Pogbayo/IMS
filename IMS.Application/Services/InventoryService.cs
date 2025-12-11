using IMS.Application.Interfaces;

namespace IMS.Application.Services
{
    public class InventoryService : IInventoryService
    {
        public Task AddStockAsync(Guid productId, Guid warehouseId, int quantity, Guid userId, string? note)
        {
            throw new NotImplementedException();
        }

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
