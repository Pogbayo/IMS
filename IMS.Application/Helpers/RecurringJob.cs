using Hangfire;
using IMS.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace IMS.Application.Helpers
{
    public class IRecurringJobs : IRecurringJob
    {
        private readonly IRecurringJobManager _manager;
        private readonly IServiceProvider _provider;

        public IRecurringJobs(IRecurringJobManager manager, IServiceProvider provider)
        {
            _manager = manager;
            _provider = provider;
        }

        public void Register()
        {
            using var scope = _provider.CreateScope();
            var jobService = scope.ServiceProvider.GetRequiredService<ICompanyDailyStatJob>();

            _manager.AddOrUpdate(
                "company-daily-stats-job",
                () => jobService.RunDailyStat(),
                "0 1 * * *",
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Local
                });
        }
    }
}
