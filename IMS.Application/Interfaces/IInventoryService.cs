namespace IMS.Application.Interfaces
{
    public interface IInventoryService
    {
        Task AddStockAsync(Guid productId, Guid warehouseId, int quantity, Guid userId, string? note);
        Task TransferStockAsync(Guid productId, Guid fromWarehouseId, Guid toWarehouseId, int quantity, Guid userId, string? note);
        Task AdjustStockAsync(Guid productId, Guid warehouseId, int quantity, Guid userId, string? note);
    }
}
