using IMS.Domain.Enums;
namespace IMS.Domain.Entities
{
    public class AuditLog : BaseEntity
    {
        public AuditAction Action { get; set; }
        public Guid UserId { get; set; }
        public AppUser? User { get; set; }
        public Guid CompanyId { get; set; }
        public Company Company { get; set; } = default!;
        public string? Description { get; set; }
    }
}
