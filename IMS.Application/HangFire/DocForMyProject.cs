using Hangfire;
using IMS.Application.Services;

namespace MyApp.HangfireReference
{
    public class HangfireJobsExample
    {
        // -------------------------------
        // 1️⃣ Recurring Jobs – Repeat on schedule
        // Example: run daily at 1 AM
        // Scenario: Daily stats calculation, cleanup tasks
        // -------------------------------
        [Obsolete]
        public void RecurringJobExample()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            RecurringJob.AddOrUpdate<CompanyDailyStatsJob>(
                job => job.RunDailyStat(),
                "0 1 * * *" // Cron: every day at 1:00 AM
            );
#pragma warning restore CS0618 // Type or member is obsolete

            RecurringJob.RemoveIfExists("jobId"); // remove if needed
            RecurringJob.Trigger("jobId");        // trigger manually
        }

        // -------------------------------
        // 2️⃣ Fire-and-Forget Jobs – Run once immediately
        // Scenario: trigger a job after an event, like user registration
        // -------------------------------
        public void FireAndForgetExample()
        {
            BackgroundJob.Enqueue<CompanyDailyStatsJob>(
                job => job.RunDailyStat()
            );
        }

        // -------------------------------
        // 3️⃣ Delayed Jobs – Run once after a delay
        // Scenario: Send a follow-up email 1 hour after registration
        // -------------------------------
        public void DelayedJobExample()
        {
            BackgroundJob.Schedule<CompanyDailyStatsJob>(
                job => job.RunDailyStat(),
                TimeSpan.FromHours(1) // run 1 hour later
            );
        }

        // -------------------------------
        // 4️⃣ Continuations – Run after another job finishes
        // Scenario: Process invoice only after order processing job completes
        // -------------------------------
        public void ContinuationJobExample()
        {
            var jobId = BackgroundJob.Enqueue<CompanyDailyStatsJob>(
                job => job.RunDailyStat()
            );

            BackgroundJob.ContinueJobWith<CompanyDailyStatsJob>(jobId,
                 job => job.RunDailyStat() // ✅ here 'job' is injected by Hangfire
             );
        }

        // -------------------------------
        // 5️⃣ Event-based / Manual Trigger
        // Scenario: Trigger a job when an external event happens
        // -------------------------------
        public void EventBasedJobExample()
        {
            // Trigger recurring job manually
#pragma warning disable CS0618 // Type or member is obsolete
            RecurringJob.Trigger("jobId");
#pragma warning restore CS0618 // Type or member is obsolete

            // Or enqueue a fire-and-forget job from an event
            BackgroundJob.Enqueue<CompanyDailyStatsJob>(
                job => job.RunDailyStat()
            );
        }
    }
}
