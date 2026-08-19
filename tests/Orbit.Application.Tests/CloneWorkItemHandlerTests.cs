using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.WorkItems;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.Projects;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.Tests;

public sealed class CloneWorkItemHandlerTests
{
    [Fact]
    public async Task Handle_CreatesUnassignedCopyWithPrefixedSummary()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var source = WorkItem.Create(
            tenantId, project.Id, 1, "ORB", "Investigate latency", "Some detail", WorkItemType.Bug, Priority.High,
            DateTimeOffset.UtcNow);
        source.SetDetails(
            null, null, "AC", null, Guid.NewGuid(), null, null, null, null, 5, ["backend"], [], []);
        var workItems = new WorkItemRepositoryStub(source);
        var handler = new CloneWorkItemHandler(
            new TenantContextStub(tenantId), new ProjectRepositoryStub(project), workItems, new UnitOfWorkStub(),
            TimeProvider.System);

        var clone = await handler.Handle(new CloneWorkItemCommand(source.Id), CancellationToken.None);

        Assert.Equal("Copy of Investigate latency", clone.Summary);
        Assert.Equal("ORB-1", clone.Key);
        Assert.Null(clone.AssigneeUserId);
        Assert.NotNull(workItems.Added);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class ProjectRepositoryStub(Project project) : IProjectRepository
    {
        public Task AddAsync(Project value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Project?> GetAsync(
            Guid tenantId, Guid projectId, ProjectPermission permission, CancellationToken cancellationToken) =>
            Task.FromResult<Project?>(project.Id == projectId && project.TenantId == tenantId ? project : null);
        public Task<PagedResult<Project>> ListAsync(
            Guid tenantId, ProjectPermission permission, int skip, int take, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<Project>([project], 1));
    }

    private sealed class WorkItemRepositoryStub(WorkItem workItem) : IWorkItemRepository
    {
        public WorkItem? Added { get; private set; }
        public Task AddAsync(WorkItem value, CancellationToken cancellationToken)
        {
            Added = value;
            return Task.CompletedTask;
        }
        public Task<WorkItem?> GetAsync(
            Guid tenantId, Guid workItemId, ProjectPermission permission, CancellationToken cancellationToken) =>
            Task.FromResult<WorkItem?>(workItem.Id == workItemId && workItem.TenantId == tenantId ? workItem : null);
        public Task<PagedResult<WorkItem>> ListByProjectAsync(
            Guid tenantId, Guid projectId, ProjectPermission permission, int skip, int take,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<WorkItem>([], 0));
        public Task<IReadOnlyList<WorkItem>> ListByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> workItemIds, ProjectPermission permission,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItem>>([]);
        public Task<bool> HasChildrenAsync(Guid tenantId, Guid parentWorkItemId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task RemoveAsync(WorkItem workItem, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
