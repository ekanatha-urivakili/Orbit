using System.Net;
using System.Net.Http.Json;
using Orbit.Api.Endpoints;

namespace Orbit.IntegrationTests;

public sealed class ApiVersionTests : IClassFixture<OrbitApiFactory>
{
    private readonly HttpClient _client;

    public ApiVersionTests(OrbitApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetApiVersion_ReportsCurrentAndSupportedVersions()
    {
        var response = await _client.GetAsync("/api/version");
        var info = await response.Content.ReadFromJsonAsync<VersionEndpoints.ApiVersionInfo>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("v1", info!.CurrentVersion);
        Assert.Contains("v1", info.SupportedVersions);
        Assert.Empty(info.Deprecated);
    }

    [Fact]
    public async Task AnyResponse_ReportsApiVersionHeader()
    {
        var response = await _client.GetAsync("/api/version");

        Assert.True(response.Headers.TryGetValues("Api-Version", out var values));
        Assert.Equal("v1", Assert.Single(values!));
    }

    [Fact]
    public async Task VersionedEndpoint_StillResolvesUnderTheV1Prefix()
    {
        var response = await _client.GetAsync("/api/v1/choices");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Api-Version", out var values));
        Assert.Equal("v1", Assert.Single(values!));
    }
}
