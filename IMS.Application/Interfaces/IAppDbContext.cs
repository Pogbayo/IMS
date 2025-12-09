using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IMS.Application.Interfaces
{
    public interface IAppDbContext
    {
        DbSet<AuditLog> AuditLogs { get; }
        DbSet<Company> Companies { get; }
        DbSet<Expense> Expenses { get; }
        DbSet<Product> Products { get; }
        DbSet<StockTransaction> StockTransactions { get; }
        DbSet<ProductWarehouse> ProductWarehouses { get; }
        DbSet<Supplier> Suppliers { get; }
        DbSet<Warehouse> Warehouses { get; }
        DbSet<Category> Categories { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
