namespace Orbit.Application.Caching;

/// <summary>
/// The single enforcement point for tenant-scoped cache keys (OBSERVABILITY-CACHING-ARCHITECTURE.md
/// §5.1 principle 2): no cache key may be built from raw string interpolation at the call site, so a
/// future key-format change can't silently create a cross-tenant leak. Matches the
/// "{context}:{tenant_id}:{resource}:{id}" shape AuthorizationContextCache already established.
/// </summary>
public static class TenantCacheKey
{
    public static string For(Guid tenantId, string context, string resource, string id) =>
        $"{context}:{tenantId}:{resource}:{id}";
}
