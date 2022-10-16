using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NanoCache.WebApiExample
{
    public class AppCache
    {
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache _distributedCache;

        public AppCache(IMemoryCache memoryCache, IDistributedCache distributedCache)
        {
            this._memoryCache = memoryCache;
            this._distributedCache = distributedCache;
        }

        public string GetFromMemoryCache(bool reCache = false, CancellationToken ct = default)
        {
            // Get Data
            if (reCache || !_memoryCache.TryGetValue("memory-cache-key", out string cachedData))
            {
                // Set Memory Cache
                cachedData = "Data from Memory Cache";
                this._memoryCache.Set("memory-cache-key", cachedData, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(15),
                });
            }

            // Return
            return cachedData;
        }
        public async Task<string> GetFromDistributedCache(bool reCache = false, CancellationToken ct = default)
        {
            // Get Data
            var cachedData = await _distributedCache.GetStringAsync("distributed-cache-key", ct);
            if (reCache || string.IsNullOrWhiteSpace(cachedData))
            {
                // Set Distributed Cache
                cachedData = "Data from Distributed Cache";
                await _distributedCache.SetStringAsync("distributed-cache-key", cachedData, new DistributedCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(15),
                }, ct);
            }

            // Return
            return cachedData;
        }
    }
}
