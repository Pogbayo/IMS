using Hangfire;
using Hangfire.States;
using IMS.Application.Interfaces;
using System.Linq.Expressions;

namespace IMS.Application.Helpers
{
    public class JobQueue : IJobQueue
    {
        private readonly IBackgroundJobClient _client;

        public JobQueue(IBackgroundJobClient client)
        {
            _client = client;
        }

        public void Enqueue(Expression<Action> job, string queue = "default")
        {
            _client.Enqueue(() => job.Compile().Invoke());
        }

        public void Enqueue<T>(Expression<Action<T>> job, string queue = "default")
        {
            _client.Create(job, new EnqueuedState(queue));
        }
    }
}
