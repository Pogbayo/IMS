using IMS.Application.ApiResponse;
using IMS.Application.DTO.Company;
namespace IMS.Application.Interfaces
{
    internal interface ICompanyService
    {
        Task<Result<CreatedCompanyDto>> RegisterCompanyAndAdmin(CompanyCreateDto dto);
        Task<Result<CompanyDto>> GetCompanyById(Guid companyId);
        Task<Result<string>> UpdateCompany(Guid companyId, CompanyUpdateDto dto);
        Task<Result<string>> DeleteCompany(Guid companyId);
        Task<Result<List<CompanyDto>>> GetAllCompanies(int PageSize, int PageNumber);
    }
}
