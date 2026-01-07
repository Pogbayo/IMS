using System.Collections.ObjectModel;

namespace IMS.Domain.Entities
{
    public class Company : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string CompanyEmail { get; set; } = string.Empty;
        public string CompanyLine { get; set; } = string.Empty;
        public string HeadOffice { get; set; } = string.Empty;
        public string? AdminEmail { get; set; }
        public Guid? CreatedById { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public AppUser CreatedBy { get; set; } = null!;
        public Collection<AuditLog> AuditLogs { get; set; } = new ();
        public Collection<StockTransaction> StockTransactions { get; set; } = new ();
        public Collection<Supplier> Suppliers { get; set; } = new();
        public Collection<AppUser> Users { get; set; } = new ();
        public List<Warehouse> Warehouses { get; set; } = new ();
        public Collection<Product> Products { get; set; } = new ();
        public Collection<Expense> Expenses { get; set; } = new ();
    }
}
