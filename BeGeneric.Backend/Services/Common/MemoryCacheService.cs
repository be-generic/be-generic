using BeGeneric.Backend.GenericModels;
using Microsoft.Extensions.Caching.Memory;

namespace BeGeneric.Backend.Services.Common
{
    public interface IMemoryCacheService
    {
        IPCacheResult CheckCachedIP(string ipAddress);
        void TryGetEntities(out List<Entity> entities, Func<List<Entity>> getEntities);
        void TryGetEndpoints(out List<Endpoint> endpoints, Func<List<Endpoint>> getEndpoints);
    }

    public class MemoryCacheService : IMemoryCacheService
    {
        private readonly MemoryCache memoryCache;

        private object lockObject = new object();
        private object lockObjectEndpoints = new object();

        private const string IPCacheKey = "ResetPasswordIP";

        public MemoryCacheService()
        {
            this.memoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        public IPCacheResult CheckCachedIP(string ipAddress)
        {
            if (this.memoryCache.TryGetValue(IPCacheKey + ipAddress, out int counter))
            {
                if (counter > 10)
                {
                    this.memoryCache.Set(IPCacheKey + ipAddress, counter + 1, new MemoryCacheEntryOptions() { SlidingExpiration = new TimeSpan(0, 5, 0) });

                    return counter == 11 ? IPCacheResult.PotentialAttack : IPCacheResult.IgnoringRequests;
                }

                this.memoryCache.Set(IPCacheKey + ipAddress, counter + 1, new MemoryCacheEntryOptions() { SlidingExpiration = new TimeSpan(0, 1, 0) });
            }
            else
            {
                this.memoryCache.Set(IPCacheKey + ipAddress, 1, new MemoryCacheEntryOptions() { SlidingExpiration = new TimeSpan(0, 1, 0) });
            }

            return IPCacheResult.FirstRequest;
        }

        public void TryGetEntities(out List<Entity> entities, Func<List<Entity>> getEntities)
        {
            lock (lockObject)
            {
                if (this.memoryCache.TryGetValue("entities", out List<Entity> cachedEntities))
                {
                    entities = cachedEntities;
                    return;
                }

                entities = getEntities();

#if DEBUG
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
#else
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(20));
#endif

                this.memoryCache.Set("entities", entities, cacheEntryOptions);
            }
        }

        public void TryGetEndpoints(out List<Endpoint> endpoints, Func<List<Endpoint>> getEndpoints)
        {
            lock (lockObjectEndpoints)
            {
                if (this.memoryCache.TryGetValue("endpoints", out List<Endpoint> cachedEntities))
                {
                    endpoints = cachedEntities;
                }

                endpoints = getEndpoints();

#if DEBUG
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
#else
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(20));
#endif

                this.memoryCache.Set("endpoints", endpoints, cacheEntryOptions);
            }
        }
    }

    public enum IPCacheResult
    {
        FirstRequest,
        PotentialAttack,
        IgnoringRequests,
    }
}
