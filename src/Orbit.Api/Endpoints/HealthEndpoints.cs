using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Orbit.Infrastructure.Persistence;

namespace Orbit.Api.Endpoints;

public static class HealthEndpoints
{
    private const string CacheProbeKey = "health:probe";

    // §4.4 point 3: bounded independent of Railway's healthcheckTimeout, so a slow-but-alive
    // dependency degrades to a fast 503 rather than hanging the whole check window.
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);

    public static async Task<IResult> ReadyAsync(
        OrbitDbContext dbContext,
        IDistributedCache cache,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ProbeTimeout);
        var probeToken = timeoutCts.Token;

        try
        {
            var dbConnected = await dbContext.Database.CanConnectAsync(probeToken);
            if (!dbConnected)
            {
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Database unavailable");
            }

            // The authorization cache (Orbit.Infrastructure.Authorization.AuthorizationContextCache)
            // reads through this same IDistributedCache on every tenant-scoped request - if the
            // backing store is unreachable, or accepts writes but fails reads, that path fails, so
            // exercise both directions here rather than reporting ready while authenticated traffic
            // is about to start failing. Because IDistributedCache falls back to
            // AddDistributedMemoryCache() when ConnectionStrings:Redis is unset, this round-trip is
            // meaningful in every environment as written - it exercises whatever is actually
            // registered rather than assuming Valkey specifically.
            await cache.SetStringAsync(
                CacheProbeKey, DateTimeOffset.UtcNow.ToString("O"),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) },
                probeToken);
            var cachedValue = await cache.GetStringAsync(CacheProbeKey, probeToken);
            if (string.IsNullOrEmpty(cachedValue))
            {
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Cache unavailable");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Either a genuine dependency failure, or the probe's own timeout fired (as opposed to
            // the caller disconnecting) - both mean "not ready", not an unhandled 500.
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Dependency unavailable");
        }

        return Results.Ok(new { status = "ready" });
    }
}
