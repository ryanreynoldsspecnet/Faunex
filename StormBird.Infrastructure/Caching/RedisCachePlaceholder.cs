using Microsoft.Extensions.Caching.Distributed;

namespace StormBird.Infrastructure.Caching;

public sealed class RedisCachePlaceholder(IDistributedCache cache) : IRedisCache
{
    public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default) =>
        await cache.GetStringAsync(key, cancellationToken);

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        cache.RemoveAsync(key, cancellationToken);

    public Task SetStringAsync(string key, string value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        var options = ttl is null
            ? new DistributedCacheEntryOptions()
            : new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl };

        return cache.SetStringAsync(key, value, options, cancellationToken);
    }
}
