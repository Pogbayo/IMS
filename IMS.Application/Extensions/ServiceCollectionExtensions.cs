using Hangfire;
using IMS.Application.Helpers;
using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
using IMS.Application.Services;
using IMS.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IMS.Application.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {

            services.AddScoped<IImageService, ImageService>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<ICompanyCalculations, CompanyCalculations>();
            services.AddScoped<ICompanyDailyStatJob, CompanyDailyStatsJob>();
            services.AddScoped<ICustomMemoryCache, CustomMemoryCache>();
            //services.AddScoped<IProductService, ProductService>();
            services.AddScoped<ICompanyService, CompanyService>();
            services.AddScoped<ISupplierService, SupplierService>();
            services.AddScoped<IProductWarehouseService, ProductWarehouseService>();
            //services.AddScoped<IStockTransactionService, StockTransactionService>();
            services.AddScoped<IWarehouseService, WarehouseService>();
            services.AddScoped<IAuditService, AuditService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IJobQueue, JobQueue>();
            services.AddScoped<IPhoneValidator, PhoneValidator>();
            services.AddScoped<IRecurringJob, IRecurringJobs>();


            services.AddScoped<IProductService>(sp =>
                new ProductService(
                    sp.GetRequiredService<IJobQueue>(),
                    sp.GetRequiredService<ICustomMemoryCache>(),
                    () => sp.GetRequiredService<IStockTransactionService>(), // lazy loading
                    sp.GetRequiredService<IImageService>(),
                    sp.GetRequiredService<UserManager<AppUser>>(),
                    sp.GetRequiredService<ILogger<ProductService>>(),
                    sp.GetRequiredService<IAppDbContext>(),
                    sp.GetRequiredService<IAuditService>(),
                    sp.GetRequiredService<ICurrentUserService>()
                ));

            services.AddScoped<IStockTransactionService>(sp =>
                new StockTransactionService(
                    sp.GetRequiredService<IAuditService>(),
                    () => sp.GetRequiredService<IProductService>(), // lazy loading
                    sp.GetRequiredService<IAppDbContext>(),
                    sp.GetRequiredService<ILogger<StockTransactionService>>(),
                    sp.GetRequiredService<ICurrentUserService>(),
                    sp.GetRequiredService<ICustomMemoryCache>()
                ));


            //RecurringJob.AddOrUpdate<CompanyDailyStatsJob>(
            //     "company-daily-stats-job",  
            //     job => job.RunDailyStat(),
            //     "0 1 * * *",
            //     new RecurringJobOptions
            //     {
            //         TimeZone = TimeZoneInfo.Local      
            //     });

            return services;
        }
    }
}
