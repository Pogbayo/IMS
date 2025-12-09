using IMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace IMS.Infrastructure.DbFactory
{
    public class IMS_DbContextFactory : IDesignTimeDbContextFactory<IMS_DbContext>
    {
        public IMS_DbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddUserSecrets("8a19281a-c644-401a-af27-ec36cfb657ce") // <-- copy from your .csproj of IMS.API
                .Build();

            // Get the connection string
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            var optionsBuilder = new DbContextOptionsBuilder<IMS_DbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new IMS_DbContext(optionsBuilder.Options);
        }
    }
}
