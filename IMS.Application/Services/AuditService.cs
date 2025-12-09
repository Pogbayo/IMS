using IMS.Domain.Entities; 
using IMS.Application.Interfaces.IAudit;
using IMS.Application.Interfaces;
using IMS.Domain.Enums;


namespace IMS.Application.Services
{
    public class AuditService : IAuditService
    {
        private readonly IAppDbContext _db;
        public AuditService(IAppDbContext db)
        {
            _db = db;
        }

        public async Task LogAsync(Guid userId, Guid companyId, AuditAction action , string description)
        {
            var log = new AuditLog
            {
                UserId = userId,
                CompanyId = companyId,
                Action = action,
                Description = description
            };

            await _db.AuditLogs.AddAsync(log);
            await _db.SaveChangesAsync();
        }
    }
}
