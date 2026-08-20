using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Api.Idempotency;
using Orbit.Api.Tenancy;
using Orbit.Application.Abstractions;
using Orbit.Domain.Configuration;
using Orbit.Domain.WorkItems;
using Orbit.Infrastructure.Persistence;

namespace Orbit.IntegrationTests;

/// <summary>
/// Covers the <see cref="IdempotencyKeyFilter"/> contract wrapping work item, sprint, and project
/// creation: a replayed <c>Idempotency-Key</c> returns the original response instead of
/// re-executing the mutation, independent keys are independent, and a key collision across tenants
/// cannot leak data between them.
/// </summary>
public sealed class IdempotencyTests : IClassFixture<OrbitApiFactory>
{
    private readonly OrbitApiFactory _factory;
    private readonly HttpClient _client;

    public IdempotencyTests(OrbitApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateWorkItem_SameIdempotencyKeyReplayed_ReturnsOriginalResponseWithoutDuplicating()
    {
        var tenantId = Guid.NewGuid();
        var project = await CreateProjectAsync(tenantId, "IDA");
        var idempotencyKey = Guid.NewGuid().ToString();

        var first = await CreateWorkItemAsync(tenantId, project.Id, "First attempt", idempotencyKey);
        var second = await CreateWorkItemAsync(tenantId, project.Id, "First attempt", idempotencyKey);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var firstBody = await first.Content.ReadFromJsonAsync<WorkItemIdDto>();
        var secondBody = await second.Content.ReadFromJsonAsync<WorkItemIdDto>();
        Assert.Equal(firstBody!.Id, secondBody!.Id);

        var count = await CountWorkItemsAsync(tenantId, project.Id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreateWorkItem_DifferentIdempotencyKeys_CreateIndependentWorkItems()
    {
        var tenantId = Guid.NewGuid();
        var project = await CreateProjectAsync(tenantId, "IDB");

        var first = await CreateWorkItemAsync(tenantId, project.Id, "One", Guid.NewGuid().ToString());
        var second = await CreateWorkItemAsync(tenantId, project.Id, "Two", Guid.NewGuid().ToString());

        var firstBody = await first.Content.ReadFromJsonAsync<WorkItemIdDto>();
        var secondBody = await second.Content.ReadFromJsonAsync<WorkItemIdDto>();
        Assert.NotEqual(firstBody!.Id, secondBody!.Id);

        var count = await CountWorkItemsAsync(tenantId, project.Id);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task CreateWorkItem_SameKeyAcrossTenants_DoesNotLeakBetweenTenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var projectA = await CreateProjectAsync(tenantA, "IDC");
        var projectB = await CreateProjectAsync(tenantB, "IDD");
        var sharedKey = Guid.NewGuid().ToString();

        var responseA = await CreateWorkItemAsync(tenantA, projectA.Id, "Tenant A card", sharedKey);
        var responseB = await CreateWorkItemAsync(tenantB, projectB.Id, "Tenant B card", sharedKey);

        var bodyA = await responseA.Content.ReadFromJsonAsync<WorkItemIdDto>();
        var bodyB = await responseB.Content.ReadFromJsonAsync<WorkItemIdDto>();

        Assert.NotEqual(bodyA!.Id, bodyB!.Id);
        Assert.Equal(1, await CountWorkItemsAsync(tenantA, projectA.Id));
        Assert.Equal(1, await CountWorkItemsAsync(tenantB, projectB.Id));
    }

    [Fact]
    public async Task CreateSprint_SameIdempotencyKeyReplayed_ReturnsOriginalResponseWithoutDuplicating()
    {
        var tenantId = Guid.NewGuid();
        var project = await CreateProjectAsync(tenantId, "IDE");
        var idempotencyKey = Guid.NewGuid().ToString();

        var first = await CreateSprintAsync(tenantId, project.Id, "Sprint 1", idempotencyKey);
        var second = await CreateSprintAsync(tenantId, project.Id, "Sprint 1", idempotencyKey);

        var firstBody = await first.Content.ReadFromJsonAsync<SprintIdDto>();
        var secondBody = await second.Content.ReadFromJsonAsync<SprintIdDto>();
        Assert.Equal(firstBody!.Id, secondBody!.Id);
    }

    [Fact]
    public async Task CreateProject_SameIdempotencyKeyReplayed_ReturnsOriginalResponseWithoutDuplicating()
    {
        var tenantId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid().ToString();

        var first = await CreateProjectWithKeyAsync(tenantId, "IDF", idempotencyKey);
        var second = await CreateProjectWithKeyAsync(tenantId, "IDF", idempotencyKey);

        var firstBody = await first.Content.ReadFromJsonAsync<ProjectDto>();
        var secondBody = await second.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.Equal(firstBody!.Id, secondBody!.Id);
    }

    private async Task<HttpResponseMessage> CreateProjectWithKeyAsync(Guid tenantId, string key, string idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/projects")
        {
            Content = JsonContent.Create(new { key, name = $"Idempotent project {key}" }),
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request.Headers.Add(IdempotencyKeyFilter.HeaderName, idempotencyKey);
        var response = await _client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return response;
    }

    [Fact]
    public async Task TryReserveAsync_ExpiredRecord_IsReclaimedByANewReservation()
    {
        var tenantId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenantId);
        var dbContext = scope.ServiceProvider.GetRequiredService<OrbitDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IIdempotencyRecordRepository>();
        var key = Guid.NewGuid().ToString();
        var path = "/api/v1/expiry-test";

        await using (var transaction = await dbContext.Database.BeginTransactionAsync())
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT set_config('app.tenant_id', {tenantId.ToString()}, true)");

            var now = DateTimeOffset.UtcNow;
            var alreadyExpired = now - TimeSpan.FromSeconds(1);

            var firstReservation = await repository.TryReserveAsync(
                tenantId, key, path, now, alreadyExpired, CancellationToken.None);
            Assert.True(firstReservation);

            await transaction.CommitAsync();
        }

        await using (var transaction = await dbContext.Database.BeginTransactionAsync())
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT set_config('app.tenant_id', {tenantId.ToString()}, true)");

            // The first reservation's expiry is already in the past, so a fresh attempt reclaims it
            // instead of losing to the (expired) conflicting row.
            var secondReservation = await repository.TryReserveAsync(
                tenantId, key, path, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(24),
                CancellationToken.None);
            Assert.True(secondReservation);

            await transaction.CommitAsync();
        }
    }

    [Fact]
    public async Task TryReserveAsync_LiveRecord_IsNotReclaimed()
    {
        var tenantId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenantId);
        var dbContext = scope.ServiceProvider.GetRequiredService<OrbitDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IIdempotencyRecordRepository>();
        var key = Guid.NewGuid().ToString();
        var path = "/api/v1/live-test";

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.tenant_id', {tenantId.ToString()}, true)");

        var now = DateTimeOffset.UtcNow;
        var first = await repository.TryReserveAsync(tenantId, key, path, now, now.AddHours(24), CancellationToken.None);
        var second = await repository.TryReserveAsync(tenantId, key, path, now, now.AddHours(24), CancellationToken.None);

        Assert.True(first);
        Assert.False(second);

        await transaction.CommitAsync();
    }

    private async Task<HttpResponseMessage> CreateWorkItemAsync(
        Guid tenantId, Guid projectId, string summary, string idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/work-items")
        {
            Content = JsonContent.Create(new
            {
                projectId,
                summary,
                description = (string?)null,
                type = "Task",
                priority = "Medium",
            }),
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request.Headers.Add(IdempotencyKeyFilter.HeaderName, idempotencyKey);
        var response = await _client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return response;
    }

    private async Task<HttpResponseMessage> CreateSprintAsync(
        Guid tenantId, Guid projectId, string name, string idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/projects/{projectId}/sprints")
        {
            Content = JsonContent.Create(new { name }),
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request.Headers.Add(IdempotencyKeyFilter.HeaderName, idempotencyKey);
        var response = await _client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return response;
    }

    private async Task<int> CountWorkItemsAsync(Guid tenantId, Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenantId);
        var dbContext = scope.ServiceProvider.GetRequiredService<OrbitDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.tenant_id', {tenantId.ToString()}, true)");
        return await dbContext.WorkItems
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId)
            .CountAsync();
    }

    /// <summary>
    /// Work item creation validates against the tenant's seeded type registry (§13.5); a bare
    /// X-Tenant-Id header used directly against a fresh random tenant has no registry rows yet.
    /// </summary>
    private async Task<ProjectDto> CreateProjectAsync(Guid tenantId, string key)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrbitDbContext>();
        await dbContext.WorkItemTypeDefinitions.AddRangeAsync(
            WorkItemTypeDefinition.CreateSoftwareDefaults(tenantId, DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();

        using var projectRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/projects")
        {
            Content = JsonContent.Create(new { key, name = $"Idempotency test {key}" }),
        };
        projectRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var projectResponse = await _client.SendAsync(projectRequest);
        Assert.True(projectResponse.IsSuccessStatusCode, await projectResponse.Content.ReadAsStringAsync());
        return (await projectResponse.Content.ReadFromJsonAsync<ProjectDto>())!;
    }

    private sealed record ProjectDto(Guid Id, string Key, string Name);

    private sealed record WorkItemIdDto(Guid Id);

    private sealed record SprintIdDto(Guid Id);
}
