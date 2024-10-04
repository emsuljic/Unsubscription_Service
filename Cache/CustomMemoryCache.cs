using Microsoft.Extensions.Caching.Memory;
using UnsubscribeService.Interfaces;

namespace UnsubscribeService.Cache
{
    public class CustomMemoryCache : ICustomMemoryCache
    {

        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<CustomMemoryCache> _logger;

        public CustomMemoryCache(IMemoryCache memoryCache, ILogger<CustomMemoryCache> logger)
        {
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public void Set<T>(string key, T value, TimeSpan duration)
        {
            _logger.LogInformation($"Setting cache for key: {key}");
            _memoryCache.Set(key, value, duration);
        }

        public bool TryGetValue<T>(string key, out T value)
        {
            var result = _memoryCache.TryGetValue(key, out object cacheValue);
            value = (T?)cacheValue;

            if (result)
            {
                _logger.LogInformation($"Cache hit for key: {key}");
            }
            else
            {
                _logger.LogWarning($"Cache miss for key: {key}");
            }

            return result;
        }

        public void Remove(string key)
        {
            _logger.LogInformation($"Removing cache for key: {key}");
            _memoryCache.Remove(key);
        }
    }
}
