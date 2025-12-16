using IMS.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace IMS.Application.Services
{
    public class CustomMemoryCache : ICustomMemoryCache
    {
        private readonly IMemoryCache _memoryCache;
        private readonly HashSet<string> _cacheKeys = new();

        public CustomMemoryCache(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        public void Remove(string key)
        {
            _memoryCache.Remove(key);
            _cacheKeys.Remove(key);
        }

        public void RemoveByPrefix(string prefix)
        {
            var keysToRemove = _cacheKeys.Where(k => k.StartsWith(prefix)).ToList();
            foreach (var key in keysToRemove)
            {
                _memoryCache.Remove(key);
                _cacheKeys.Remove(key);
            }
        }

        public void Set(string key, object value, MemoryCacheEntryOptions options)
        {
            _memoryCache.Set(key, value, options);
            _cacheKeys.Add(key);
        }

        public bool TryGetValue<T>(string key, out T value)
        {
            return _memoryCache.TryGetValue(key, out value!);
        }

        public async Task<T?> GetOrCreateAsync<T>( string key,Func<ICacheEntry, Task<T?>> factory)
        {
            if (_memoryCache.TryGetValue(key, out T? cachedValue))
                return cachedValue;

            using var entry = _memoryCache.CreateEntry(key);
            var value = await factory(entry);

            if (value == null)
            {
                _memoryCache.Remove(key);
                return default;
            }

            entry.Value = value;
            return value;
        }
    }
}
