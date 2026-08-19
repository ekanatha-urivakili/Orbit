using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Api.Tenancy;
using Orbit.Application.Abstractions;
using Orbit.Domain.Configuration;
using Orbit.Domain.WorkItems;
using Orbit.Infrastructure.Persistence;

namespace Orbit.IntegrationTests;

/// <summary>
/// Regression test for the <c>WatchWorkItemHandler</c> idempotency race (WorkItemWatchers.cs): two
/// concurrent inserts of the same (tenant, work item, user) watcher must both succeed rather than
/// one hitting an unhandled unique-constraint violation on
/// <c>ux_work_item_watchers_tenant_item_user</c>.
/// </summary>
public sealed class WorkItemWatcherConcurrencyTests : IClassFixture<OrbitApiFactory>
{
    private readonly OrbitApiFactory _factory;
    private readonly HttpClient _client;

    public WorkItemWatcherConcurrencyTests(OrbitApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AddAsync_ConcurrentDuplicateWatchers_BothSucceedWithOneRow()
    {
        var tenantId = Guid.NewGuid();
        var workItemId = await CreateWorkItem(tenantId);
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var first = WorkItemWatcher.Create(tenantId, workItemId, userId, now);
        var second = WorkItemWatcher.Create(tenantId, workItemId, userId, now);

        await Task.WhenAll(
            InsertWatcherAsync(tenantId, first),
            InsertWatcherAsync(tenantId, second));

        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenantId);
        var dbContext = scope.ServiceProvider.GetRequiredService<OrbitDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.tenant_id', {tenantId.ToString()}, true)");
        var count = await dbContext.WorkItemWatchers
            .Where(watcher => watcher.TenantId == tenantId
                && watcher.WorkItemId == workItemId
                && watcher.UserId == userId)
            .CountAsync();
        Assert.Equal(1, count);
    }

    private async Task InsertWatcherAsync(Guid tenantId, WorkItemWatcher watcher)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrbitDbContext>();
        // set_config(..., true) is transaction-scoped, so it and the insert must share an explicit
        // transaction (mirroring TenantTransactionMiddleware, which the real request path runs under).
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.tenant_id', {tenantId.ToString()}, true)");
        var repository = scope.ServiceProvider.GetRequiredService<IWorkItemWatcherRepository>();
        await repository.AddAsync(watcher, CancellationToken.None);
        await transaction.CommitAsync();
    }

    /// <summary>
    /// Work item creation validates against the tenant's seeded type registry (§13.5); a bare
    /// <c>X-Tenant-Id</c> header used directly against a fresh random tenant, without going through
    /// workspace bootstrap, has no registry rows yet.
    /// </summary>
    private async Task SeedWorkItemTypeRegistry(Guid tenantId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrbitDbContext>();
        await dbContext.WorkItemTypeDefinitions.AddRangeAsync(
            WorkItemTypeDefinition.CreateSoftwareDefaults(tenantId, DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    private async Task<Guid> CreateWorkItem(Guid tenantId)
    {
        await SeedWorkItemTypeRegistry(tenantId);

        using var projectRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/projects")
        {
            Content = JsonContent.Create(new { key = "WCT", name = "Watcher concurrency project" }),
        };
        projectRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var projectResponse = await _client.SendAsync(projectRequest);
        projectResponse.EnsureSuccessStatusCode();
        var project = await projectResponse.Content.ReadFromJsonAsync<ProjectDto>();

        using var workItemRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/work-items")
        {
            Content = JsonContent.Create(new
            {
                projectId = project!.Id,
                summary = "Watched card",
                description = (string?)null,
                type = "Task",
                priority = "Medium",
            }),
        };
        workItemRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var workItemResponse = await _client.SendAsync(workItemRequest);
        Assert.True(workItemResponse.IsSuccessStatusCode, await workItemResponse.Content.ReadAsStringAsync());
        // Deserialize only the id: WorkItemDto's enum properties use the API's configured
        // string-enum JSON converter, which this bare HttpClient doesn't have registered.
        var workItem = await workItemResponse.Content.ReadFromJsonAsync<WorkItemIdDto>();
        return workItem!.Id;
    }

    private sealed record ProjectDto(Guid Id, string Key, string Name);

    private sealed record WorkItemIdDto(Guid Id);
}
