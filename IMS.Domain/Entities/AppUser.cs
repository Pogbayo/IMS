using Microsoft.AspNetCore.Identity;

namespace IMS.Domain.Entities
{
    public class AppUser : IdentityUser<Guid>
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } 
        public string? ImageUrl { get; set; }
        public DateTime DeletedAt{get;set;}
        public bool IsUpdated { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsCompanyAdmin { get; set; } = false;
        public Guid? CompanyId { get; set; }
        public Company? Company { get; set; } = default!;
        public Company? CreatedCompany { get; set; }
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
        public void MarkAsDeleted()
        {
            IsDeleted = true;
            DeletedAt = DateTime.UtcNow;
        }
        public void MarkAsUpdated()
        {
            IsUpdated = true;
            DeletedAt = DateTime.UtcNow;
        }
    }
}
