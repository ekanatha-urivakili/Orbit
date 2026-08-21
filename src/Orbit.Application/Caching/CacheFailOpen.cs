using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Orbit.Application.Caching;

/// <summary>
/// OBSERVABILITY-CACHING-ARCHITECTURE.md §5.1 principle 5: a cache read/write failure must be
/// caught, logged (picked up automatically by the CorrelationId/TraceId log scope), and treated as
/// a cache miss - never surfaced as a 5xx. Every HybridCache consumer under this document goes
/// through here rather than calling HybridCache.GetOrCreateAsync directly.
/// </summary>
public static class CacheFailOpen
{
    public static async Task<T> GetOrCreateAsync<T>(
        HybridCache cache,
        ILogger logger,
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options,
        CancellationToken cancellationToken)
    {
        try
        {
            return await cache.GetOrCreateAsync(key, factory, options, cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            CacheTelemetry.FailOpenTotal.Add(1);
            logger.LogWarning(exception, "Cache unavailable for {CacheKey}; loading directly", key);
            return await factory(cancellationToken);
        }
    }
}
