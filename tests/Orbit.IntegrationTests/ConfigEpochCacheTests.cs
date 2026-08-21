using System.Net.Http.Json;
using System.Text.Json;

namespace Orbit.IntegrationTests;

/// <summary>
/// Exercises the HybridCache-backed status catalog read (ListWorkItemStatusesHandler) through the
/// real API and Postgres, proving Project.ConfigEpoch invalidation actually surfaces a write on the
/// very next read rather than serving a stale cached page (OBSERVABILITY-CACHING-ARCHITECTURE.md
/// §5.1 principle 3/7).
/// </summary>
public sealed class ConfigEpochCacheTests : IClassFixture<OrbitApiFactory>
{
    private readonly HttpClient _client;

    public ConfigEpochCacheTests(OrbitApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListStatuses_ReflectsANewlyCreatedStatus_AfterAnEarlierCachedRead()
    {
        var tenantId = Guid.NewGuid();
        var projectId = await CreateProject(tenantId);

        var before = await ListStatusKeys(tenantId, projectId);
        Assert.DoesNotContain("ready-for-qa", before);

        await CreateStatus(tenantId, projectId);

        var after = await ListStatusKeys(tenantId, projectId);
        Assert.Contains("ready-for-qa", after);
    }

    private async Task<Guid> CreateProject(Guid tenantId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/projects")
        {
            Content = JsonContent.Create(new { key = "CEC", name = "Config epoch cache project" }),
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<List<string>> ListStatusKeys(Guid tenantId, Guid projectId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/projects/{projectId}/statuses");
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return [.. body.RootElement.EnumerateArray().Select(status => status.GetProperty("key").GetString()!)];
    }

    private async Task CreateStatus(Guid tenantId, Guid projectId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/projects/{projectId}/statuses")
        {
            Content = JsonContent.Create(new
            {
                key = "ready-for-qa",
                name = "Ready for QA",
                category = "InProgress",
                order = 45,
                colorToken = "purple",
            }),
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }
}
