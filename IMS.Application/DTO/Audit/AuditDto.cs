using IMS.Domain.Enums;

namespace IMS.Application.DTO.Audit
{
    public class AuditDto
    {
        public Guid UserId { get; set; }
        public string? UserName { get; set; }
        public Guid CompanyId { get; set; }
        public AuditAction Action { get; set; }
        public required string Description { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
