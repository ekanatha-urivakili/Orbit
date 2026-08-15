using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Orbit.Application.Common;
using Orbit.Application.Projects;

namespace Orbit.IntegrationTests;

/// <summary>
/// Proves that reusing a pooled Npgsql connection across requests for different tenants does not
/// leak PostgreSQL RLS context between them. <c>TenantTransactionMiddleware</c> (Orbit.Api.Tenancy)
/// sets <c>app.tenant_id</c> with <c>set_config(..., true)</c>, i.e. transaction-local: it resets
/// automatically when each request's transaction commits, regardless of whether the underlying
/// physical connection returns to the pool and is handed to the very next (different-tenant)
/// request. <c>Maximum Pool Size=1</c> forces exactly that: every request in this test reuses the
/// same physical connection, one at a time.
/// </summary>
public sealed class ConnectionReuseIsolationTests : IClassFixture<SinglePooledConnectionApiFactory>
{
    private readonly HttpClient _client;

    public ConnectionReuseIsolationTests(SinglePooledConnectionApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AlternatingTenantRequests_OnAReusedConnection_NeverSeeEachOthersData()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await CreateProject(tenantA, "AAA", "Tenant A Project");
        await CreateProject(tenantB, "BBB", "Tenant B Project");

        for (var iteration = 0; iteration < 5; iteration++)
        {
            var projectsForA = await ListProjectNames(tenantA);
            Assert.Contains("Tenant A Project", projectsForA);
            Assert.DoesNotContain("Tenant B Project", projectsForA);

            var projectsForB = await ListProjectNames(tenantB);
            Assert.Contains("Tenant B Project", projectsForB);
            Assert.DoesNotContain("Tenant A Project", projectsForB);
        }
    }

    private async Task CreateProject(Guid tenantId, string key, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/projects")
        {
            Content = JsonContent.Create(new { key, name }),
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private async Task<IReadOnlyList<string>> ListProjectNames(Guid tenantId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/projects");
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ProjectDto>>();
        return page!.Items.Select(project => project.Name).ToArray();
    }
}

public sealed class SinglePooledConnectionApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Same POSTGRES_PASSWORD/orbit_local default as deploy/podman/compose.yaml: this test needs
        // a real connection (unlike ApiContractTests' placeholder password, which never actually
        // reaches Postgres) to exercise genuine connection-pool reuse across tenants.
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "orbit_local";
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "ConnectionStrings:Postgres",
            $"Host=localhost;Database=orbit_test;Username=orbit;Password={password};Maximum Pool Size=1");
        builder.UseSetting("Tenancy:AllowHeaderTenant", "true");
        builder.UseSetting("DatabaseSecurity:EnforceRuntimeRole", "false");
    }
}
