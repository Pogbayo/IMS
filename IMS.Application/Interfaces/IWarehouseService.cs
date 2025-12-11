using IMS.Application.ApiResponse;
using IMS.Application.DTO;
using IMS.Application.DTO.Warehouse;
namespace IMS.Application.Interfaces
{
    public interface IWarehouseService
    {
        Task<Result<Guid>> CreateWarehouse(CreateWarehouseDto dto);
        Task<Result<string>> UpdateWarehouse(Guid warehouseId, UpdateWarehouseDto dto);
        Task<Result<string>> DeleteWarehouse(Guid warehouseId);
        Task<Result<string>> MarkAsInActive(Guid warehouseId);
        Task<Result<string>> MarkAsActive(Guid warehouseId);
        Task<Result<WarehouseDto>> GetWarehouseById(Guid warehouseId);
        Task<Result<List<WarehouseDto>>> GetWarehouses(Guid companyId, int PageNumber, int Pagesize);
        Task<Result<List<WarehouseDto>>> GetWarehousesContainingProduct(Guid productId);
    }
}
