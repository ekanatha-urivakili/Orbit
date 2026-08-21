using Orbit.Application.Caching;

namespace Orbit.IntegrationTests;

public sealed class TenantCacheKeyTests
{
    [Fact]
    public void For_BuildsTheContextTenantResourceIdShape()
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var key = TenantCacheKey.For(tenantId, "config", "statuses", "abc:v3");

        Assert.Equal("config:11111111-1111-1111-1111-111111111111:statuses:abc:v3", key);
    }

    [Fact]
    public void For_DifferentTenantsProduceDifferentKeys_ForTheSameResourceAndId()
    {
        var first = TenantCacheKey.For(Guid.NewGuid(), "config", "statuses", "same-id");
        var second = TenantCacheKey.For(Guid.NewGuid(), "config", "statuses", "same-id");

        Assert.NotEqual(first, second);
    }
}
