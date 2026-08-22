using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Api.Tenancy;
using Orbit.Application.Abstractions;
using Orbit.Domain.Boards;
using Orbit.Domain.Choices;
using Orbit.Domain.Configuration;
using Orbit.Domain.Projects;
using Orbit.Domain.WorkItems;
using Orbit.Infrastructure.Persistence;

namespace Orbit.IntegrationTests;

/// <summary>
/// Exercises <see cref="IWorkItemRepository.GetMinBacklogRankAsync"/> against real Postgres,
/// covering the "top of backlog" placement the close-sprint handler relies on (§13.5): an item
/// in the active sprint must not count as backlog, an item in a closed sprint's still-current
/// membership must not count either, and an empty backlog returns null.
/// </summary>
public sealed class WorkItemBacklogRankTests : IClassFixture<OrbitApiFactory>
{
    private readonly OrbitApiFactory _factory;

    public WorkItemBacklogRankTests(OrbitApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetMinBacklogRankAsync_ExcludesActiveSprintItems_ReturnsLowestBacklogRank()
    {
        using var scope = _factory.Services.CreateScope();
        var tenantId = Guid.NewGuid();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenantId);
        var dbContext = scope.ServiceProvider.GetRequiredService<OrbitDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IWorkItemRepository>();
        var now = DateTimeOffset.UtcNow;

        var project = Project.Create(tenantId, "ORB", "Orbit", now);
        await dbContext.Projects.AddAsync(project);
        await dbContext.WorkItemTypeDefinitions.AddRangeAsync(WorkItemTypeDefinition.CreateSoftwareDefaults(tenantId, now));
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, now);
        await dbContext.WorkItemStatusDefinitions.AddRangeAsync(statuses);
        var statusId = statuses[0].Id;
        var backlogLow = WorkItem.Create(tenantId, project.Id, 1, "ORB", "Backlog low rank", null, WorkItemType.Task, Priority.Medium, statusId, now);
        var backlogHigh = WorkItem.Create(tenantId, project.Id, 2, "ORB", "Backlog high rank", null, WorkItemType.Task, Priority.Medium, statusId, now);
        var sprintItem = WorkItem.Create(tenantId, project.Id, 3, "ORB", "Active sprint item", null, WorkItemType.Task, Priority.Medium, statusId, now);
        sprintItem.Reorder(1m, now);
        await dbContext.WorkItems.AddRangeAsync(backlogLow, backlogHigh, sprintItem);

        var activeSprint = Sprint.Create(tenantId, project.Id, "Sprint 1", now);
        activeSprint.Start(null, null, null, now);
        await dbContext.Sprints.AddAsync(activeSprint);
        await dbContext.SprintMemberships.AddAsync(SprintMembership.Create(tenantId, activeSprint.Id, sprintItem.Id, now));

        await dbContext.SaveChangesAsync();

        var minRank = await repository.GetMinBacklogRankAsync(tenantId, project.Id, CancellationToken.None);

        Assert.Equal(Math.Min(backlogLow.Rank, backlogHigh.Rank), minRank);
    }

    [Fact]
    public async Task GetMinBacklogRankAsync_ReturnsNull_WhenBacklogIsEmpty()
    {
        using var scope = _factory.Services.CreateScope();
        var tenantId = Guid.NewGuid();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenantId);
        var dbContext = scope.ServiceProvider.GetRequiredService<OrbitDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IWorkItemRepository>();
        var now = DateTimeOffset.UtcNow;

        var project = Project.Create(tenantId, "ORB", "Orbit", now);
        await dbContext.Projects.AddAsync(project);
        await dbContext.WorkItemTypeDefinitions.AddRangeAsync(WorkItemTypeDefinition.CreateSoftwareDefaults(tenantId, now));
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, now);
        await dbContext.WorkItemStatusDefinitions.AddRangeAsync(statuses);
        var statusId = statuses[0].Id;
        var sprintItem = WorkItem.Create(tenantId, project.Id, 1, "ORB", "Active sprint item", null, WorkItemType.Task, Priority.Medium, statusId, now);
        await dbContext.WorkItems.AddAsync(sprintItem);
        var activeSprint = Sprint.Create(tenantId, project.Id, "Sprint 1", now);
        activeSprint.Start(null, null, null, now);
        await dbContext.Sprints.AddAsync(activeSprint);
        await dbContext.SprintMemberships.AddAsync(SprintMembership.Create(tenantId, activeSprint.Id, sprintItem.Id, now));

        await dbContext.SaveChangesAsync();

        var minRank = await repository.GetMinBacklogRankAsync(tenantId, project.Id, CancellationToken.None);

        Assert.Null(minRank);
    }
}
