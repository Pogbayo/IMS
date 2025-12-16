using IMS.Application.ApiResponse;
using IMS.Domain.Enums;

namespace IMS.Application.Interfaces.IAudit
{
    public interface IAuditService
    {
        Task LogAsync(Guid userId, Guid companyId, AuditAction action, string description);
        Task<Result<dynamic>> GetAudits(Guid CompanyId, int pageSize, int pageNumber);
    }
}
