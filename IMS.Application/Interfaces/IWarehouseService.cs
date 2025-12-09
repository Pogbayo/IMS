using IMS.Application.DTO;
using IMS.Application.DTO.Warehouse;
namespace IMS.Application.Interfaces
{
    public interface IWarehouseService
    {
        Task<Guid> CreateWarehouse(CreateWarehouseDto dto);
        Task UpdateWarehouse(Guid warehouseId, UpdateWarehouseDto dto);
        Task DeleteWarehouse(Guid warehouseId);
        Task<WarehouseDto> GetWarehouseById(Guid warehouseId);
        Task<List<WarehouseDto>> GetWarehouses(Guid companyId);

        Task<List<WarehouseDto>> GetWarehousesContainingProduct(Guid productId);
    }

}
