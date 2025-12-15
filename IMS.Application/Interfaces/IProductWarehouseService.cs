using IMS.Domain.Enums;
namespace IMS.Application.Interfaces
{
    public interface IProductWarehouseService
    {
        Task UpdateProductInWarehouseAsync(
            Guid productId,
            Guid warehouseId,
            int quantityChanged,
            Guid userId,
            ProductUpdateInWarehouseType UpdateType
            );
        Task TransferProductAsync(
            Guid productId,
            Guid fromWarehouseId,
            Guid toWarehouseId,
            int quantity,
            Guid userId
            );
    }
}
