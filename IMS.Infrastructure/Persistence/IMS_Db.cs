using IMS.Application.Interfaces;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IMS.Infrastructure.Persistence
{
    public class IMS_DbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>, IAppDbContext
    {
        public IMS_DbContext(DbContextOptions<IMS_DbContext> options)
            : base(options)
        {
        }

        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<StockTransaction> StockTransactions { get; set; }
        public DbSet<ProductWarehouse> ProductWarehouses { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Warehouse> Warehouses { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<CompanyDailyStat> CompanyDailyStats { get; set; }

        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            return await Database.BeginTransactionAsync();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return base.SaveChangesAsync(cancellationToken);
        }

        public Task<int> UpdateChangesAsync<TEntity>(TEntity entity) where TEntity : class
        {
            Set<TEntity>().Update(entity);
            return base.SaveChangesAsync();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

           
            modelBuilder.Entity<AppUser>().ToTable("AppUsers", "inventory");
            modelBuilder.Entity<IdentityRole<Guid>>().ToTable("Roles", "inventory");
            modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles", "inventory");
            modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims", "inventory");
            modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins", "inventory");
            modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims", "inventory");
            modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens", "inventory");

            modelBuilder.Entity<Product>().ToTable("Products", "inventory");
            modelBuilder.Entity<AuditLog>().ToTable("AuditLogs", "inventory");
            modelBuilder.Entity<Company>().ToTable("Companies", "inventory");
            modelBuilder.Entity<Expense>().ToTable("Expenses", "inventory");
            modelBuilder.Entity<StockTransaction>().ToTable("StockTransactions", "inventory");
            modelBuilder.Entity<ProductWarehouse>().ToTable("ProductWarehouses", "inventory");
            modelBuilder.Entity<Supplier>().ToTable("Suppliers", "inventory");
            modelBuilder.Entity<CompanyDailyStat>().ToTable("CompanyDailyStats", "inventory");
            modelBuilder.Entity<Warehouse>().ToTable("Warehouses", "inventory");
            modelBuilder.Entity<Category>().ToTable("Categories", "inventory");

            modelBuilder.Entity<AppUser>().HasQueryFilter(u => !u.IsDeleted);
            modelBuilder.Entity<Company>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<Warehouse>().HasQueryFilter(w => !w.IsDeleted);
            modelBuilder.Entity<Product>().HasQueryFilter(p => !p.IsDeleted);
            modelBuilder.Entity<ProductWarehouse>().HasQueryFilter(s => !s.IsDeleted);
            modelBuilder.Entity<Supplier>().HasQueryFilter(sp => !sp.IsDeleted);
            modelBuilder.Entity<Expense>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<AuditLog>().HasQueryFilter(a => !a.IsDeleted);
            modelBuilder.Entity<Category>().HasQueryFilter(a => !a.IsDeleted);
            modelBuilder.Entity<CompanyDailyStat>().HasQueryFilter(a => !a.IsDeleted);
            modelBuilder.Entity<StockTransaction>() .HasQueryFilter(st => !st.Company.IsDeleted);

            modelBuilder.Entity<AuditLog>()
                .Property(a => a.Action)
                .HasConversion(
                    v => v.ToString(),              
                    v => Enum.Parse<AuditAction>(v) 
                );

            modelBuilder.Entity<Expense>()
                .Property(e => e.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Product>()
                .Property(p => p.RetailPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.SKU)
                .IsUnique();

            modelBuilder.Entity<Product>()
                .Property(p => p.CostPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Product>()
                .Property(p => p.Profit)
                .HasPrecision(18, 2);

            modelBuilder.Entity<CompanyDailyStat>()
                .Property(c => c.TotalInventoryValue)
                .HasPrecision(18, 2);


            // Company => AppUser (regular users in company) - NO ACTION
            modelBuilder.Entity<AppUser>()
                .HasOne(u => u.Company)
                .WithMany(c => c.Users)
                .HasForeignKey(u => u.CompanyId)
                .OnDelete(DeleteBehavior.NoAction);  // Changed from Restrict to NoAction

            // Company => AppUser (creator) - NO ACTION
            // This is the relationship causing the cascade path issue
            modelBuilder.Entity<Company>()
                .HasOne(c => c.CreatedBy)
                .WithOne(u => u.CreatedCompany)
                .HasForeignKey<Company>(c => c.CreatedById)
                .OnDelete(DeleteBehavior.NoAction);  // Changed from Restrict to NoAction

            // AuditLog => Company (who performed the action)
            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.Company)
                .WithMany(c => c.AuditLogs)
                .HasForeignKey(a => a.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            // AuditLog => AppUser (optional - for tracking specific user)
            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.NoAction)  // NoAction to avoid cascade conflicts
                .IsRequired(false);

            // Product => ProductWarehouse
            modelBuilder.Entity<Product>()
                .HasMany(p => p.ProductWarehouses)
                .WithOne(s => s.Product)
                .HasForeignKey(s => s.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // ProductWarehouse => StockTransaction
            modelBuilder.Entity<StockTransaction>()
                .HasOne(t => t.ProductWarehouse)
                .WithMany(pw => pw.StockTransactions)
                .HasForeignKey(t => t.ProductWarehouseId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            // StockTransaction => AppUser
            modelBuilder.Entity<StockTransaction>()
                .HasOne(st => st.User)
                .WithMany()
                .HasForeignKey(st => st.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);  // Changed to NoAction

            // Warehouse => ProductWarehouse
            modelBuilder.Entity<Warehouse>()
                .HasMany(w => w.ProductWarehouses)
                .WithOne(pw => pw.Warehouse)
                .HasForeignKey(pw => pw.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            // Company => Suppliers
            modelBuilder.Entity<Company>()
                .HasMany(c => c.Suppliers)
                .WithOne(s => s.Company)
                .HasForeignKey(s => s.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            // Product => Category
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Company => Warehouses
            modelBuilder.Entity<Warehouse>()
                .HasOne(w => w.Company)
                .WithMany(c => c.Warehouses)
                .HasForeignKey(w => w.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            // Company => Products
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Company)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            // Company => Expenses
            modelBuilder.Entity<Expense>()
                .HasOne(e => e.Company)
                .WithMany(c => c.Expenses)
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            // Company => StockTransactions
            modelBuilder.Entity<StockTransaction>()
                .HasOne(st => st.Company)
                .WithMany(c => c.StockTransactions)
                .HasForeignKey(st => st.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
