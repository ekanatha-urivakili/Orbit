using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.WorkItems;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.Projects;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.Tests;

public sealed class MoveWorkItemHandlerTests
{
    [Fact]
    public async Task Handle_ReassignsProjectAndGeneratesNewKey()
    {
        var tenantId = Guid.NewGuid();
        var sourceProject = Project.Create(tenantId, "SRC", "Source", DateTimeOffset.UtcNow);
        var targetProject = Project.Create(tenantId, "TGT", "Target", DateTimeOffset.UtcNow);
        var workItem = WorkItem.Create(
            tenantId, sourceProject.Id, 1, "SRC", "Move this card", null, WorkItemType.Task, Priority.Medium,
            DateTimeOffset.UtcNow);
        var history = new WorkItemHistoryRepositoryStub();
        var handler = new MoveWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new ProjectRepositoryStub(sourceProject, targetProject),
            new WorkItemRepositoryStub(workItem),
            history,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new MoveWorkItemCommand(workItem.Id, targetProject.Id, workItem.Version), CancellationToken.None);

        Assert.Equal(targetProject.Id, result.ProjectId);
        Assert.Equal("TGT-1", result.Key);
        Assert.Single(history.Entries);
        Assert.Equal("Project", history.Entries[0].FieldName);
        Assert.Equal("SRC", history.Entries[0].OldValue);
        Assert.Equal("TGT", history.Entries[0].NewValue);
    }

    [Fact]
    public async Task Handle_SameProject_IsNoOp()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "SRC", "Source", DateTimeOffset.UtcNow);
        var workItem = WorkItem.Create(
            tenantId, project.Id, 1, "SRC", "Stay put", null, WorkItemType.Task, Priority.Medium,
            DateTimeOffset.UtcNow);
        var history = new WorkItemHistoryRepositoryStub();
        var handler = new MoveWorkItemHandler(
            new TenantContextStub(tenantId), new CurrentPrincipalStub(),
            new ProjectRepositoryStub(project, project),
            new WorkItemRepositoryStub(workItem), history, new UnitOfWorkStub(), TimeProvider.System);

        var result = await handler.Handle(
            new MoveWorkItemCommand(workItem.Id, project.Id, workItem.Version), CancellationToken.None);

        Assert.Equal("SRC-1", result.Key);
        Assert.Equal(1, result.Version);
        Assert.Empty(history.Entries);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class CurrentPrincipalStub : ICurrentPrincipal
    {
        public Guid? UserId => null;
        public Guid? SessionId => null;
        public Guid MembershipId => Guid.NewGuid();
        public PrincipalType PrincipalType => PrincipalType.User;
        public TenantRole TenantRole => TenantRole.Owner;
        public MembershipTier MembershipTier => MembershipTier.Standard;
        public bool IsDevelopmentBypass => true;
    }

    private sealed class WorkItemHistoryRepositoryStub : IWorkItemHistoryRepository
    {
        public List<WorkItemHistoryEntry> Entries { get; } = [];
        public Task AddAsync(WorkItemHistoryEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
        public Task<PagedResult<WorkItemHistoryEntry>> ListByWorkItemAsync(
            Guid tenantId, Guid workItemId, int skip, int take, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<WorkItemHistoryEntry>(Entries, Entries.Count));
    }

    private sealed class ProjectRepositoryStub(params Project[] projects) : IProjectRepository
    {
        public Task AddAsync(Project value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Project?> GetAsync(
            Guid tenantId, Guid projectId, ProjectPermission permission, CancellationToken cancellationToken) =>
            Task.FromResult(projects.SingleOrDefault(p => p.Id == projectId && p.TenantId == tenantId));
        public Task<PagedResult<Project>> ListAsync(
            Guid tenantId, ProjectPermission permission, int skip, int take, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<Project>(projects, projects.Length));
    }

    private sealed class WorkItemRepositoryStub(WorkItem workItem) : IWorkItemRepository
    {
        public Task AddAsync(WorkItem value, CancellationToken cancellationToken) => Task.CompletedTask;
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
