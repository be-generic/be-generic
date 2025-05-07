using BeGeneric.Backend.Common.Models;
using Microsoft.Extensions.Caching.Memory;

namespace BeGeneric.Backend.Services.Common;

public interface IMemoryCacheService
{
    IPCacheResult CheckCachedIP(string ipAddress);
    void TryGetEntities(out List<Entity> entities, Func<List<Entity>> getEntities);
}

public class MemoryCacheService : IMemoryCacheService
{
    private readonly MemoryCache memoryCache;

    private readonly object lockObject = new();
    private readonly object lockObjectEndpoints = new();

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
}

public enum IPCacheResult
{
    FirstRequest,
    PotentialAttack,
    IgnoringRequests,
}
