using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Orbit.Infrastructure.Persistence;

namespace Orbit.Api.Endpoints;

public static class HealthEndpoints
{
    private const string CacheProbeKey = "health:probe";

    public static async Task<IResult> ReadyAsync(
        OrbitDbContext dbContext,
        IDistributedCache cache,
        CancellationToken cancellationToken)
    {
        var dbConnected = await dbContext.Database.CanConnectAsync(cancellationToken);
        if (!dbConnected)
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Database unavailable");
        }

        try
        {
            // The authorization cache (Orbit.Infrastructure.Authorization.AuthorizationContextCache)
            // reads through this same IDistributedCache on every tenant-scoped request - if the
            // backing store is unreachable that path throws, so surface it here rather than
            // reporting ready while authenticated traffic is about to start failing.
            await cache.SetStringAsync(
                CacheProbeKey, DateTimeOffset.UtcNow.ToString("O"),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) },
                cancellationToken);
        }
        catch
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Cache unavailable");
        }

        return Results.Ok(new { status = "ready" });
    }
}
