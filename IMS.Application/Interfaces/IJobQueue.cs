using System.Linq.Expressions;

namespace IMS.Application.Interfaces
{
    public interface IJobQueue
    {
        void Enqueue(Expression<Action> job, string queue = "default");
        void Enqueue<T>(Expression<Action<T>> job, string queue = "default");
    }
}
