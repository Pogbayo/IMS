namespace IMS.Domain.Entities
{
    public abstract class BaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool? IsUpdated { get; set; } = false;
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public bool IsActive { get; set; } = false;
        public DateTime? MadeActive { get; set; }

        public string PhoneNumber { get; set; } = string.Empty;

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

        public void MarkAsActive()
        {
            IsActive = true;
            MadeActive = DateTime.UtcNow;
        }
        public void MarkAsInActive()
        {
            IsActive = false;
            MadeActive = DateTime.UtcNow;
        }
    }
}
