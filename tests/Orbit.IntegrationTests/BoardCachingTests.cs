using System.Net.Http.Json;
using System.Text.Json;

namespace Orbit.IntegrationTests;

/// <summary>
/// Exercises the HybridCache-backed board read (GetBoardHandler) through the real API and
/// Postgres, proving Board.Epoch invalidation surfaces a config write on the very next read
/// (OBSERVABILITY-CACHING-ARCHITECTURE.md §5.2 row 1) rather than serving a stale cached page.
/// </summary>
public sealed class BoardCachingTests : IClassFixture<OrbitApiFactory>
{
    private readonly HttpClient _client;

    public BoardCachingTests(OrbitApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetBoard_ReflectsARenameMadeAfterAnEarlierCachedRead()
    {
        var tenantId = Guid.NewGuid();
        var projectId = await CreateProject(tenantId);

        // First PATCH materializes the Board row (If-Match: "0" per allowZero: true).
        await UpdateBoard(tenantId, projectId, "Delivery Board", version: 0);

        var before = await GetBoardName(tenantId, projectId);
        var currentVersion = await GetBoardVersion(tenantId, projectId);
        await UpdateBoard(tenantId, projectId, "Renamed Board", currentVersion);

        var after = await GetBoardName(tenantId, projectId);

        Assert.Equal("Delivery Board", before);
        Assert.Equal("Renamed Board", after);
    }

    private async Task<Guid> CreateProject(Guid tenantId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/projects")
        {
            Content = JsonContent.Create(new { key = "BCC", name = "Board caching project" }),
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task UpdateBoard(Guid tenantId, Guid projectId, string name, long version)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/projects/{projectId}/board")
        {
            Content = JsonContent.Create(new { name, type = "Kanban", columns = (object?)null }),
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request.Headers.Add("If-Match", $"\"{version}\"");
        var response = await _client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task<string> GetBoardName(Guid tenantId, Guid projectId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/projects/{projectId}/board");
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("name").GetString()!;
    }

    private async Task<long> GetBoardVersion(Guid tenantId, Guid projectId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/projects/{projectId}/board");
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("version").GetInt64();
    }
}
