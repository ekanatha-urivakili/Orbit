using System.Net;

namespace Orbit.IntegrationTests;

public sealed class HealthEndpointsTests : IClassFixture<OrbitApiFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointsTests(OrbitApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Live_AlwaysReportsHealthy()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_ReportsReadyWhenDatabaseAndCacheAreReachable()
    {
        // Testing env has no Redis connection string, so IDistributedCache falls back to
        // AddDistributedMemoryCache() - the read-back is still exercised against that.
        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
