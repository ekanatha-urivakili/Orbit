using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.WorkItems;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.Tests;

public sealed class ArchiveWorkItemHandlerTests
{
    [Fact]
    public async Task Archive_ThenUnarchive_RoundTrips()
    {
        var tenantId = Guid.NewGuid();
        var workItem = WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Archive this card", null, WorkItemType.Task, Priority.Medium,
            DateTimeOffset.UtcNow);
        var history = new WorkItemHistoryRepositoryStub();
        var archiveHandler = new ArchiveWorkItemHandler(
            new TenantContextStub(tenantId), new CurrentPrincipalStub(),
            new WorkItemRepositoryStub(workItem), history, new UnitOfWorkStub(),
            TimeProvider.System);

        var archived = await archiveHandler.Handle(
            new ArchiveWorkItemCommand(workItem.Id, workItem.Version), CancellationToken.None);
        Assert.True(archived.IsArchived);
        Assert.Single(history.Entries);
        Assert.Equal("Archived", history.Entries[0].FieldName);
        Assert.Equal("Yes", history.Entries[0].NewValue);

        var unarchiveHandler = new UnarchiveWorkItemHandler(
            new TenantContextStub(tenantId), new CurrentPrincipalStub(),
            new WorkItemRepositoryStub(workItem), history, new UnitOfWorkStub(),
            TimeProvider.System);
        var unarchived = await unarchiveHandler.Handle(
            new UnarchiveWorkItemCommand(workItem.Id, archived.Version), CancellationToken.None);

        Assert.False(unarchived.IsArchived);
        Assert.Equal(2, history.Entries.Count);
        Assert.Equal("Archived", history.Entries[1].FieldName);
        Assert.Equal("No", history.Entries[1].NewValue);
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
        public Task<IReadOnlyList<WorkItemHistoryEntry>> ListByWorkItemAsync(
            Guid tenantId, Guid workItemId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItemHistoryEntry>>(Entries);
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
