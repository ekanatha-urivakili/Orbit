using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Orbit.Domain.Access;
using Orbit.Infrastructure.Authorization;

namespace Orbit.IntegrationTests;

public sealed class AuthorizationContextCacheTests
{
    private static AuthorizationContextCache NewCache() =>
        new(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

    [Fact]
    public async Task GetAsync_ReturnsNull_OnMiss()
    {
        var cache = NewCache();

        var result = await cache.GetAsync(Guid.NewGuid(), Guid.NewGuid(), 1, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsTheSameContext_ForTheSameEpoch()
    {
        var cache = NewCache();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var context = new CachedAuthorizationContext(Guid.NewGuid(), userId, PrincipalType.User, TenantRole.Administrator);

        await cache.SetAsync(tenantId, userId, epoch: 1, context, CancellationToken.None);
        var result = await cache.GetAsync(tenantId, userId, epoch: 1, CancellationToken.None);

        Assert.Equal(context, result);
    }

    [Fact]
    public async Task GetAsync_Misses_WhenEpochChanges()
    {
        var cache = NewCache();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var context = new CachedAuthorizationContext(Guid.NewGuid(), userId, PrincipalType.User, TenantRole.Member);

        await cache.SetAsync(tenantId, userId, epoch: 1, context, CancellationToken.None);
        var result = await cache.GetAsync(tenantId, userId, epoch: 2, CancellationToken.None);

        Assert.Null(result);
    }
}
