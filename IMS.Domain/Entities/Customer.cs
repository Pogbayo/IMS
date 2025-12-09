using System.ComponentModel.DataAnnotations;

namespace IMS.Domain.Entities
{
    public class Customer : BaseEntity
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
