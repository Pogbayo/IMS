namespace IMS.Domain.Entities
{
    public class Supplier : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public Guid CompanyId { get; set; }
        public Company Company { get; set; } = default!;
        public bool IsActive { get; set; } = false;
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
