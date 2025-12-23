using IMS.Application.ApiResponse;
using IMS.Application.DTO.Audit;
using IMS.Domain.Enums;

namespace IMS.Application.Interfaces.IAudit
{
    public interface IAuditService
    {
        Task LogAsync(Guid userId, Guid companyId, AuditAction action, string description);
        Task<Result<List<AuditDto>>> GetAudits(Guid CompanyId, int pageSize, int pageNumber);
    }
}
