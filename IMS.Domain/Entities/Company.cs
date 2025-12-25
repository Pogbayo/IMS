namespace IMS.Domain.Entities
{
    public class Company : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string CompanyLine { get; set; } = string.Empty;
        public string HeadOffice { get; set; } = string.Empty;
        public Guid? CreatedById { get; set; }
        public AppUser CreatedBy { get; set; } = null!;
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
        public ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();
        public ICollection<Supplier> Suppliers { get; set; } = new List<Supplier>();
        public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
        public List<Warehouse> Warehouses { get; set; } = new List<Warehouse>();
        public ICollection<Product> Products { get; set; } = new List<Product>();
        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    }
}
