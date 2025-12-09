using IMS.Application.DTO.ProductWarehouse;
namespace IMS.Application.Interfaces
{
    public interface IProductWarehouseService
    {
        Task<ProductWarehouseDto> GetStockLevel(Guid warehouseId, Guid productId);
        Task<List<ProductWarehouseDto>> GetProductsInWarehouse(Guid warehouseId);
    }
}
