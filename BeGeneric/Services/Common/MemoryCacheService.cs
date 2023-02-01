using Microsoft.Extensions.Caching.Memory;
using System;

namespace BeGeneric.Services.Common
{
    public interface IMemoryCacheService
    {
        IPCacheResult CheckCachedIP(string ipAddress);
    }

    public class MemoryCacheService : IMemoryCacheService
    {
        private readonly MemoryCache memoryCache;

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
    }

    public enum IPCacheResult
    {
        FirstRequest,
        PotentialAttack,
        IgnoringRequests,
    }
}
