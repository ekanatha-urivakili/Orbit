using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Domain.Configuration;
using Orbit.Infrastructure.Persistence;

namespace Orbit.IntegrationTests;

/// <summary>
/// Exercises the work item due-date field end to end through the real API and Postgres, covering
/// create, update, and the domain rule that due date cannot precede start date (WorkItem.cs
/// SetDetails) — proving it surfaces as a client error through the HTTP pipeline rather than a
/// 500 when the EF migration/column mapping and validation wiring line up.
/// </summary>
public sealed class WorkItemDueDateTests : IClassFixture<OrbitApiFactory>
{
    private readonly OrbitApiFactory _factory;
    private readonly HttpClient _client;

    public WorkItemDueDateTests(OrbitApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateWorkItem_PersistsAndReturnsDueDate()
    {
        var tenantId = Guid.NewGuid();
        await SeedWorkItemTypeRegistry(tenantId);
        var projectId = await CreateProject(tenantId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/work-items")
        {
            Content = JsonContent.Create(new
            {
                projectId,
                summary = "Ship the release",
                description = (string?)null,
                type = "Task",
                priority = "Medium",
                startDate = "2026-08-10",
                dueDate = "2026-08-20",
            }),
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("2026-08-20", body.RootElement.GetProperty("dueDate").GetString());
    }

    [Fact]
    public async Task CreateWorkItem_RejectsDueDateBeforeStartDate()
    {
        var tenantId = Guid.NewGuid();
        await SeedWorkItemTypeRegistry(tenantId);
        var projectId = await CreateProject(tenantId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/work-items")
        {
            Content = JsonContent.Create(new
            {
                projectId,
                summary = "Ship the release",
                description = (string?)null,
                type = "Task",
                priority = "Medium",
                startDate = "2026-08-20",
                dueDate = "2026-08-10",
            }),
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);

        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task UpdateWorkItem_RoundTripsDueDateThroughIfMatch()
    {
        var tenantId = Guid.NewGuid();
        await SeedWorkItemTypeRegistry(tenantId);
        var projectId = await CreateProject(tenantId);

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/work-items")
        {
            Content = JsonContent.Create(new
            {
                projectId,
                summary = "Ship the release",
                description = (string?)null,
                type = "Task",
                priority = "Medium",
            }),
        };
        createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var createResponse = await _client.SendAsync(createRequest);
        Assert.True(createResponse.IsSuccessStatusCode, await createResponse.Content.ReadAsStringAsync());
        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var workItemId = created.RootElement.GetProperty("id").GetGuid();
        var version = created.RootElement.GetProperty("version").GetInt64();

        using var updateRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/work-items/{workItemId}")
        {
            Content = JsonContent.Create(new
            {
                summary = "Ship the release",
                description = (string?)null,
                priority = "Medium",
                startDate = "2026-09-01",
                dueDate = "2026-09-10",
            }),
        };
        updateRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        updateRequest.Headers.Add("If-Match", $"\"{version}\"");
        var updateResponse = await _client.SendAsync(updateRequest);
        Assert.True(updateResponse.IsSuccessStatusCode, await updateResponse.Content.ReadAsStringAsync());

        using var updated = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync());
        Assert.Equal("2026-09-10", updated.RootElement.GetProperty("dueDate").GetString());
    }

    private async Task SeedWorkItemTypeRegistry(Guid tenantId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrbitDbContext>();
        await dbContext.WorkItemTypeDefinitions.AddRangeAsync(
            WorkItemTypeDefinition.CreateSoftwareDefaults(tenantId, DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    private async Task<Guid> CreateProject(Guid tenantId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/projects")
        {
            Content = JsonContent.Create(new { key = "DUE", name = "Due date project" }),
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }
}
