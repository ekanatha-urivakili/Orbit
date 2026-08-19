using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.WorkItems;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.Common;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.Tests;

public sealed class ChangeWorkItemTypeHandlerTests
{
    [Fact]
    public async Task Handle_ValidTypeChange_PersistsAndReturnsUpdatedDto()
    {
        var tenantId = Guid.NewGuid();
        var workItem = WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Reclassify this card", null, WorkItemType.Task,
            Priority.Medium, DateTimeOffset.UtcNow);
        var handler = new ChangeWorkItemTypeHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new WorkItemRepositoryStub(workItem),
            new WorkItemHistoryRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new ChangeWorkItemTypeCommand(workItem.Id, WorkItemType.Bug, workItem.Version),
            CancellationToken.None);

        Assert.Equal(WorkItemType.Bug, result.Type);
        Assert.Equal(WorkItemType.Bug, workItem.Type);
    }

    [Fact]
    public async Task Handle_ValidTypeChange_RecordsHistoryEntry()
    {
        var tenantId = Guid.NewGuid();
        var workItem = WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Reclassify this card", null, WorkItemType.Task,
            Priority.Medium, DateTimeOffset.UtcNow);
        var history = new WorkItemHistoryRepositoryStub();
        var handler = new ChangeWorkItemTypeHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new WorkItemRepositoryStub(workItem),
            history,
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(
            new ChangeWorkItemTypeCommand(workItem.Id, WorkItemType.Bug, workItem.Version),
            CancellationToken.None);

        var entry = Assert.Single(history.Added);
        Assert.Equal("Type", entry.FieldName);
        Assert.Equal("Task", entry.OldValue);
        Assert.Equal("Bug", entry.NewValue);
    }

    [Fact]
    public async Task Handle_StaleVersion_ThrowsConcurrencyException()
    {
        var tenantId = Guid.NewGuid();
        var workItem = WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Reclassify this card", null, WorkItemType.Task,
            Priority.Medium, DateTimeOffset.UtcNow);
        var handler = new ChangeWorkItemTypeHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new WorkItemRepositoryStub(workItem),
            new WorkItemHistoryRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new ChangeWorkItemTypeCommand(workItem.Id, WorkItemType.Bug, workItem.Version + 1),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConcurrencyException>(action);
    }

    [Fact]
    public async Task Handle_StructuralTargetType_ThrowsDomainException()
    {
        var tenantId = Guid.NewGuid();
        var workItem = WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Reclassify this card", null, WorkItemType.Task,
            Priority.Medium, DateTimeOffset.UtcNow);
        var handler = new ChangeWorkItemTypeHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new WorkItemRepositoryStub(workItem),
            new WorkItemHistoryRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new ChangeWorkItemTypeCommand(workItem.Id, WorkItemType.Epic, workItem.Version),
            CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(action);
    }

    [Fact]
    public async Task Handle_UnknownWorkItem_ThrowsNotFoundException()
    {
        var tenantId = Guid.NewGuid();
        var workItem = WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Reclassify this card", null, WorkItemType.Task,
            Priority.Medium, DateTimeOffset.UtcNow);
        var handler = new ChangeWorkItemTypeHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new WorkItemRepositoryStub(workItem),
            new WorkItemHistoryRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new ChangeWorkItemTypeCommand(Guid.NewGuid(), WorkItemType.Bug, 1),
            CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
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

    private sealed class WorkItemRepositoryStub(WorkItem workItem) : IWorkItemRepository
    {
        public Task AddAsync(WorkItem value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<WorkItem?> GetAsync(
            Guid tenantId,
            Guid workItemId,
            ProjectPermission permission,
            CancellationToken cancellationToken) =>
            Task.FromResult<WorkItem?>(workItem.Id == workItemId && workItem.TenantId == tenantId ? workItem : null);
        public Task<PagedResult<WorkItem>> ListByProjectAsync(
            Guid tenantId,
            Guid projectId,
            ProjectPermission permission,
            int skip,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<WorkItem>([], 0));
        public Task<IReadOnlyList<WorkItem>> ListByIdsAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> workItemIds,
            ProjectPermission permission,
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

    private sealed class WorkItemHistoryRepositoryStub : IWorkItemHistoryRepository
    {
        public List<WorkItemHistoryEntry> Added { get; } = [];

        public Task AddAsync(WorkItemHistoryEntry entry, CancellationToken cancellationToken)
        {
            Added.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WorkItemHistoryEntry>> ListByWorkItemAsync(
            Guid tenantId, Guid workItemId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItemHistoryEntry>>([]);
    }
}
