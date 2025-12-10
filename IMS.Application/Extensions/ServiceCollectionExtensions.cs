using Hangfire;
using IMS.Application.Helpers;
using IMS.Application.Interfaces;
using IMS.Application.Services;
using Microsoft.Extensions.DependencyInjection;

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

            RecurringJob.AddOrUpdate<CompanyDailyStatsJob>(
                 "company-daily-stats-job",  
                 job => job.RunDailyStat(),
                 "0 1 * * *",
                 new RecurringJobOptions
                 {
                     TimeZone = TimeZoneInfo.Local      
                 });

            return services;
        }
    }
}
