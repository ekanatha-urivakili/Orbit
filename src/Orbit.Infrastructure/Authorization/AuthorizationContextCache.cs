using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Orbit.Domain.Access;

namespace Orbit.Infrastructure.Authorization;

public sealed record CachedAuthorizationContext(
    Guid MembershipId,
    Guid? UserId,
    PrincipalType PrincipalType,
    TenantRole TenantRole);

/// <summary>
/// Caches the per-request principal context resolved from a locally-issued user token, keyed by
/// the tenant's <see cref="Domain.Workspaces.Workspace.AuthorizationEpoch"/>. A permission-affecting
/// mutation bumps the epoch, so a stale entry simply becomes unreachable under a new key rather
/// than needing explicit invalidation - the TTL below is a memory bound, not the correctness
/// mechanism. The API's tenant transaction middleware is the read-through consumer.
/// </summary>
public interface IAuthorizationContextCache
{
    Task<CachedAuthorizationContext?> GetAsync(
        Guid tenantId, Guid userId, long epoch, CancellationToken cancellationToken);

    Task SetAsync(
        Guid tenantId, Guid userId, long epoch, CachedAuthorizationContext context, CancellationToken cancellationToken);
}

public sealed class AuthorizationContextCache(IDistributedCache cache) : IAuthorizationContextCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public async Task<CachedAuthorizationContext?> GetAsync(
        Guid tenantId, Guid userId, long epoch, CancellationToken cancellationToken)
    {
        var value = await cache.GetStringAsync(Key(tenantId, userId, epoch), cancellationToken);
        return value is null ? null : JsonSerializer.Deserialize<CachedAuthorizationContext>(value);
    }

    public async Task SetAsync(
        Guid tenantId, Guid userId, long epoch, CachedAuthorizationContext context, CancellationToken cancellationToken) =>
        await cache.SetStringAsync(
            Key(tenantId, userId, epoch),
            JsonSerializer.Serialize(context),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl },
            cancellationToken);

    private static string Key(Guid tenantId, Guid userId, long epoch) => $"authz:{tenantId}:{userId}:{epoch}";
}
