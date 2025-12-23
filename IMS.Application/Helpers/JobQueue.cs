using Hangfire;
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

        public void Enqueue(Expression<Action> job)
        {
            _client.Enqueue(job);
        }

        public void Enqueue<T>(Expression<Action<T>> job)
        {
            _client.Enqueue(job);
        }
    }

}
