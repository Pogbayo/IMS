using IMS.Application.ApiResponse;

namespace IMS.Application.Interfaces
{
    internal interface ISupplierService
    {
        Task<Result<string>> RegisterSupplierToCompany(Guid companyId, SupplierCreateDto dto);
        Task<Result<List<SupplierDto>>> GetAllSuppliers();
        Task<Result<string>> UpdateSupplier(SupplierUpdateDto dto);
        Task<Result<string>> DeleteSupplier(Guid supplierId);
    }
}
