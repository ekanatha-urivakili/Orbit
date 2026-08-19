using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.WorkItems;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.Common;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.Tests;

public sealed class WorkItemWatcherHandlerTests
{
    [Fact]
    public async Task Watch_AddsWatcherForCurrentUser()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var workItem = CreateWorkItem(tenantId);
        var watchers = new WorkItemWatcherRepositoryStub();
        var handler = new WatchWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(userId),
            new WorkItemRepositoryStub(workItem),
            watchers,
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(new WatchWorkItemCommand(workItem.Id), CancellationToken.None);

        var added = Assert.Single(watchers.Added);
        Assert.Equal(userId, added.UserId);
        Assert.Equal(workItem.Id, added.WorkItemId);
    }

    [Fact]
    public async Task Watch_IsIdempotent_WhenAlreadyWatching()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var workItem = CreateWorkItem(tenantId);
        var existing = WorkItemWatcher.Create(tenantId, workItem.Id, userId, DateTimeOffset.UtcNow);
        var watchers = new WorkItemWatcherRepositoryStub(existing);
        var handler = new WatchWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(userId),
            new WorkItemRepositoryStub(workItem),
            watchers,
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(new WatchWorkItemCommand(workItem.Id), CancellationToken.None);

        Assert.Empty(watchers.Added);
    }

    [Fact]
    public async Task Watch_HidesExistence_WhenWorkItemNotVisible()
    {
        var tenantId = Guid.NewGuid();
        var handler = new WatchWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(Guid.NewGuid()),
            new WorkItemRepositoryStub(null),
            new WorkItemWatcherRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(new WatchWorkItemCommand(Guid.NewGuid()), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    [Fact]
    public async Task Unwatch_RemovesExistingWatcher()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();
        var existing = WorkItemWatcher.Create(tenantId, workItemId, userId, DateTimeOffset.UtcNow);
        var watchers = new WorkItemWatcherRepositoryStub(existing);
        var handler = new UnwatchWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(userId),
            watchers,
            new UnitOfWorkStub());

        await handler.Handle(new UnwatchWorkItemCommand(workItemId), CancellationToken.None);

        Assert.Contains(existing, watchers.Removed);
    }

    [Fact]
    public async Task Unwatch_IsIdempotent_WhenNotWatching()
    {
        var tenantId = Guid.NewGuid();
        var watchers = new WorkItemWatcherRepositoryStub();
        var handler = new UnwatchWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(Guid.NewGuid()),
            watchers,
            new UnitOfWorkStub());

        await handler.Handle(new UnwatchWorkItemCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(watchers.Removed);
    }

    [Fact]
    public async Task GetWatchers_ReflectsCurrentUsersWatchStateAndTotalCount()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var workItem = CreateWorkItem(tenantId);
        var watchers = new WorkItemWatcherRepositoryStub(
            WorkItemWatcher.Create(tenantId, workItem.Id, userId, DateTimeOffset.UtcNow),
            WorkItemWatcher.Create(tenantId, workItem.Id, Guid.NewGuid(), DateTimeOffset.UtcNow));
        var handler = new GetWorkItemWatchersHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(userId),
            new WorkItemRepositoryStub(workItem),
            watchers);

        var result = await handler.Handle(new GetWorkItemWatchersQuery(workItem.Id), CancellationToken.None);

        Assert.True(result.IsWatching);
        Assert.Equal(2, result.Count);
    }

    private static WorkItem CreateWorkItem(Guid tenantId) =>
        WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Build the board", null, WorkItemType.Story, Priority.High,
            DateTimeOffset.UtcNow);

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class CurrentPrincipalStub(Guid? userId) : ICurrentPrincipal
    {
        public Guid? UserId => userId;
        public Guid? SessionId => null;
        public Guid MembershipId => Guid.NewGuid();
        public PrincipalType PrincipalType => PrincipalType.User;
        public TenantRole TenantRole => TenantRole.Member;
        public MembershipTier MembershipTier => MembershipTier.Standard;
        public bool IsDevelopmentBypass => true;
    }

    private sealed class WorkItemRepositoryStub(WorkItem? workItem) : IWorkItemRepository
    {
        public Task AddAsync(WorkItem value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<WorkItem?> GetAsync(
            Guid tenantId,
            Guid workItemId,
            ProjectPermission permission,
            CancellationToken cancellationToken) =>
            Task.FromResult(workItem is not null && workItem.Id == workItemId && workItem.TenantId == tenantId
                ? workItem
                : null);
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
    }

    private sealed class WorkItemWatcherRepositoryStub(params WorkItemWatcher[] watchers) : IWorkItemWatcherRepository
    {
        private readonly List<WorkItemWatcher> current = [.. watchers];

        public List<WorkItemWatcher> Added { get; } = [];
        public List<WorkItemWatcher> Removed { get; } = [];

        public Task AddAsync(WorkItemWatcher watcher, CancellationToken cancellationToken)
        {
            current.Add(watcher);
            Added.Add(watcher);
            return Task.CompletedTask;
        }

        public Task<WorkItemWatcher?> GetAsync(
            Guid tenantId, Guid workItemId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(current.SingleOrDefault(
                watcher => watcher.TenantId == tenantId && watcher.WorkItemId == workItemId && watcher.UserId == userId));

        public Task<IReadOnlyList<WorkItemWatcher>> ListByWorkItemAsync(
            Guid tenantId, Guid workItemId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItemWatcher>>(
                [.. current.Where(watcher => watcher.TenantId == tenantId && watcher.WorkItemId == workItemId)]);

        public Task RemoveAsync(WorkItemWatcher watcher, CancellationToken cancellationToken)
        {
            current.Remove(watcher);
            Removed.Add(watcher);
            return Task.CompletedTask;
        }
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
