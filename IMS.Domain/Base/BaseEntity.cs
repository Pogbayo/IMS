using System.ComponentModel.DataAnnotations;

namespace IMS.Domain.Entities
{
    public abstract class BaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool? IsUpdated { get; set; } = false;
        public bool IsDeleted { get; set; } = false;

        private string _phoneNumber = string.Empty;
        public DateTime? DeletedAt { get; set; }

        public void MarkAsUpdated()
        {
            IsUpdated = true;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MarkAsDeleted()
        {
            IsDeleted = true;
            DeletedAt = DateTime.UtcNow;
        }
    }
}
