using System.Net;
using Orbit.Api.Observability;

namespace Orbit.IntegrationTests;

public sealed class CorrelationIdTests : IClassFixture<OrbitApiFactory>
{
    private readonly HttpClient _client;

    public CorrelationIdTests(OrbitApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Response_EchoesAValidSentCorrelationId()
    {
        var sent = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/choices");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, sent);

        var response = await _client.SendAsync(request);

        Assert.Equal(sent, response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }

    [Fact]
    public async Task Response_ReplacesAMalformedCorrelationIdWithAFreshGuid()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/choices");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "not-a-guid");

        var response = await _client.SendAsync(request);

        var value = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();
        Assert.True(Guid.TryParse(value, out _));
        Assert.NotEqual("not-a-guid", value);
    }

    [Fact]
    public async Task Response_CarriesCorrelationIdEvenOnAPipelineShortCircuit()
    {
        // Missing X-Tenant-Id is rejected by TenantTransactionMiddleware before any handler runs
        // (400) - the header set by CorrelationIdMiddleware, upstream of it, must still be present.
        var response = await _client.GetAsync("/api/v1/projects");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.Contains(CorrelationIdMiddleware.HeaderName));
    }
}
