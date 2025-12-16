using Microsoft.Extensions.Caching.Memory;

namespace IMS.Application.Interfaces
{
    public interface ICustomMemoryCache
    {
        void Set(string key, object value, MemoryCacheEntryOptions options);
        bool TryGetValue<T>(string key, out T value);
        void Remove(string key);
        void RemoveByPrefix(string prefix);
        Task<T?> GetOrCreateAsync<T>(string key,Func<ICacheEntry, Task<T?>> factory);
    }
}
