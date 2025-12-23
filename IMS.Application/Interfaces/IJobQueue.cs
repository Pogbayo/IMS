using System.Linq.Expressions;

namespace IMS.Application.Interfaces
{
    public interface IJobQueue
    {
        void Enqueue(Expression<Action> job);
        void Enqueue<T>(Expression<Action<T>> job);
    }
}
