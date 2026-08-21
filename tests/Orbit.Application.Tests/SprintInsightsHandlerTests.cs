using Orbit.Application.Abstractions;
using Orbit.Application.Boards;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Choices;
using Orbit.Domain.Configuration;
using Orbit.Domain.Projects;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.Tests;

public sealed class SprintInsightsHandlerTests
{
    private static readonly TimeProvider Clock = TimeProvider.System;

    private static IReadOnlyList<WorkItemStatusDefinition> DefaultStatuses(Guid tenantId, Guid projectId) =>
        WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, projectId, DateTimeOffset.UtcNow);

    private static WorkItem NewItem(Guid tenantId, Guid projectId, Guid statusId, long sequenceNumber, DateTimeOffset now) =>
        WorkItem.Create(
            tenantId, projectId, sequenceNumber, "ORB", $"Card {sequenceNumber}", null,
            WorkItemType.Task, Priority.Medium, statusId, now);

    [Fact]
    public async Task Handle_ComputesProgressAndFlagsOverdueItem()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = DefaultStatuses(tenantId, project.Id);
        var backlog = statuses.Single(s => s.Key == "backlog");
        var done = statuses.Single(s => s.Key == "done");
        var now = DateTimeOffset.UtcNow;

        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", now);
        sprint.Start(null, DateOnly.FromDateTime(now.UtcDateTime), null, now);

        var overdueItem = NewItem(tenantId, project.Id, backlog.Id, 1, now);
        overdueItem.SetDetails(
            null, null, null, null, null, null, null, null, null, null,
            DateOnly.FromDateTime(now.UtcDateTime.AddDays(-5)), null, 3, null, null, null);
        var doneItem = NewItem(tenantId, project.Id, done.Id, 2, now);

        var memberships = new SprintMembershipRepositoryStub(
            SprintMembership.Create(tenantId, sprint.Id, overdueItem.Id, now),
            SprintMembership.Create(tenantId, sprint.Id, doneItem.Id, now));

        var handler = new SprintInsightsHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project),
            new SprintRepositoryStub(sprint),
            memberships,
            new SprintScopeFactRepositoryStub(),
            new WorkItemRepositoryStub(overdueItem, doneItem),
            new WorkItemStatusRepositoryStub(statuses),
            Clock);

        var result = await handler.Handle(new SprintInsightsQuery(sprint.Id), CancellationToken.None);

        Assert.Equal(2, result.TotalItems);
        Assert.Equal(1, result.DoneItems);
        Assert.Equal(50, result.PercentDone);
        var attention = Assert.Single(result.ItemsForAttention);
        Assert.Equal(overdueItem.Id, attention.WorkItemId);
        Assert.True(attention.IsOverdue);
    }

    [Fact]
    public async Task Handle_HidesExistence_WhenPrincipalCannotViewProject()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        var handler = new SprintInsightsHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, canView: false),
            new SprintRepositoryStub(sprint),
            new SprintMembershipRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            new WorkItemRepositoryStub(),
            new WorkItemStatusRepositoryStub([]),
            Clock);

        var action = () => handler.Handle(new SprintInsightsQuery(sprint.Id), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class ProjectRepositoryStub(Project project, bool canView = true) : IProjectRepository
    {
        public Task AddAsync(Project value, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Project?> GetAsync(
            Guid tenantId, Guid projectId, ProjectPermission permission, CancellationToken cancellationToken) =>
            Task.FromResult(project.Id == projectId && project.TenantId == tenantId && canView ? project : null);

        public Task<PagedResult<Project>> ListAsync(
            Guid tenantId, ProjectPermission permission, int skip, int take, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<Project>([project], 1));
    }

    private sealed class SprintRepositoryStub(params Sprint[] sprints) : ISprintRepository
    {
        public Task AddAsync(Sprint sprint, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Sprint?> GetAsync(Guid tenantId, Guid sprintId, CancellationToken cancellationToken) =>
            Task.FromResult(sprints.SingleOrDefault(sprint => sprint.Id == sprintId && sprint.TenantId == tenantId));

        public Task<Sprint?> GetActiveAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(sprints.SingleOrDefault(sprint =>
                sprint.TenantId == tenantId && sprint.ProjectId == projectId && sprint.State == SprintState.Active));

        public Task<IReadOnlyList<Sprint>> ListByProjectAsync(
            Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Sprint>>(
                [.. sprints.Where(sprint => sprint.TenantId == tenantId && sprint.ProjectId == projectId)]);
    }

    private sealed class SprintMembershipRepositoryStub(params SprintMembership[] memberships) : ISprintMembershipRepository
    {
        public Task AddAsync(SprintMembership membership, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<SprintMembership?> GetCurrentByWorkItemAsync(
            Guid tenantId, Guid workItemId, CancellationToken cancellationToken) =>
            Task.FromResult<SprintMembership?>(null);

        public Task<IReadOnlyList<SprintMembership>> ListCurrentBySprintAsync(
            Guid tenantId, Guid sprintId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SprintMembership>>(
                [.. memberships.Where(m => m.TenantId == tenantId && m.SprintId == sprintId && m.RemovedAt is null)]);

        public Task<IReadOnlyList<SprintMembership>> ListCurrentBySprintsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> sprintIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SprintMembership>>([]);
    }

    private sealed class SprintScopeFactRepositoryStub : ISprintScopeFactRepository
    {
        public Task AddAsync(SprintScopeFact fact, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<SprintScopeFact>> ListBySprintAsync(
            Guid tenantId, Guid sprintId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SprintScopeFact>>([]);
    }

    private sealed class WorkItemRepositoryStub(params WorkItem[] items) : IWorkItemRepository
    {
        public Task AddAsync(WorkItem workItem, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<WorkItem?> GetAsync(
            Guid tenantId, Guid workItemId, ProjectPermission permission, CancellationToken cancellationToken) =>
            Task.FromResult(items.SingleOrDefault(item => item.Id == workItemId && item.TenantId == tenantId));

        public Task<PagedResult<WorkItem>> ListByProjectAsync(
            Guid tenantId, Guid projectId, ProjectPermission permission, int skip, int take,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<WorkItem>([], 0));

        public Task<IReadOnlyList<WorkItem>> ListByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> workItemIds, ProjectPermission permission,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItem>>(
                [.. items.Where(item => item.TenantId == tenantId && workItemIds.Contains(item.Id))]);

        public Task<bool> HasChildrenAsync(Guid tenantId, Guid parentWorkItemId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task RemoveAsync(WorkItem workItem, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class WorkItemStatusRepositoryStub(IReadOnlyList<WorkItemStatusDefinition> statuses) : IWorkItemStatusRepository
    {
        public Task AddAsync(WorkItemStatusDefinition definition, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddRangeAsync(IReadOnlyCollection<WorkItemStatusDefinition> definitions, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<WorkItemStatusDefinition?> GetAsync(
            Guid tenantId, Guid projectId, Guid statusId, CancellationToken cancellationToken) =>
            Task.FromResult(statuses.SingleOrDefault(status => status.Id == statusId));

        public Task<IReadOnlyList<WorkItemStatusDefinition>> ListByProjectAsync(
            Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(statuses);

        public Task<WorkItemStatusDefinition?> GetDefaultAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<WorkItemStatusDefinition?>(statuses.OrderBy(status => status.Order).FirstOrDefault());

        public Task<bool> IsInUseAsync(Guid tenantId, Guid projectId, Guid statusId, string statusKey, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task RemoveAsync(WorkItemStatusDefinition definition, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
