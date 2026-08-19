using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.WorkItems;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.Common;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.Tests;

public sealed class ReorderWorkItemHandlerTests
{
    private static WorkItem NewItem(Guid tenantId, Guid projectId, long sequenceNumber) =>
        WorkItem.Create(
            tenantId, projectId, sequenceNumber, "ORB", $"Card {sequenceNumber}", null,
            WorkItemType.Task, Priority.Medium, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Handle_SetsMidpointRank_WhenBetweenTwoNeighbors()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var before = NewItem(tenantId, projectId, 1);
        var target = NewItem(tenantId, projectId, 2);
        var after = NewItem(tenantId, projectId, 3);
        var handler = new ReorderWorkItemHandler(
            new TenantContextStub(tenantId),
            new WorkItemRepositoryStub(before, target, after),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new ReorderWorkItemCommand(target.Id, before.Id, after.Id, target.Version),
            CancellationToken.None);

        Assert.Equal((before.Rank + after.Rank) / 2m, result.Rank);
    }

    [Fact]
    public async Task Handle_MovesAboveBeforeNeighbor_WhenNoAfterNeighbor()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var before = NewItem(tenantId, projectId, 1);
        var target = NewItem(tenantId, projectId, 2);
        var handler = new ReorderWorkItemHandler(
            new TenantContextStub(tenantId),
            new WorkItemRepositoryStub(before, target),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new ReorderWorkItemCommand(target.Id, before.Id, null, target.Version),
            CancellationToken.None);

        Assert.True(result.Rank > before.Rank);
    }

    [Fact]
    public async Task Handle_MovesBelowAfterNeighbor_WhenNoBeforeNeighbor()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var target = NewItem(tenantId, projectId, 1);
        var after = NewItem(tenantId, projectId, 2);
        var handler = new ReorderWorkItemHandler(
            new TenantContextStub(tenantId),
            new WorkItemRepositoryStub(target, after),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new ReorderWorkItemCommand(target.Id, null, after.Id, target.Version),
            CancellationToken.None);

        Assert.True(result.Rank < after.Rank);
    }

    [Fact]
    public async Task Handle_RejectsStaleVersion()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var before = NewItem(tenantId, projectId, 1);
        var target = NewItem(tenantId, projectId, 2);
        var handler = new ReorderWorkItemHandler(
            new TenantContextStub(tenantId),
            new WorkItemRepositoryStub(before, target),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new ReorderWorkItemCommand(target.Id, before.Id, null, target.Version + 1),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConcurrencyException>(action);
    }

    [Fact]
    public async Task Handle_RejectsNeighborFromAnotherProject()
    {
        var tenantId = Guid.NewGuid();
        var target = NewItem(tenantId, Guid.NewGuid(), 1);
        var otherProjectNeighbor = NewItem(tenantId, Guid.NewGuid(), 1);
        var handler = new ReorderWorkItemHandler(
            new TenantContextStub(tenantId),
            new WorkItemRepositoryStub(target, otherProjectNeighbor),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new ReorderWorkItemCommand(target.Id, otherProjectNeighbor.Id, null, target.Version),
            CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(action);
    }

    [Fact]
    public async Task Handle_HidesExistence_WhenWorkItemNotVisible()
    {
        var tenantId = Guid.NewGuid();
        var handler = new ReorderWorkItemHandler(
            new TenantContextStub(tenantId),
            new WorkItemRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new ReorderWorkItemCommand(Guid.NewGuid(), null, Guid.NewGuid(), 1),
            CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class WorkItemRepositoryStub(params WorkItem[] items) : IWorkItemRepository
    {
        public Task AddAsync(WorkItem workItem, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<WorkItem?> GetAsync(
            Guid tenantId,
            Guid workItemId,
            ProjectPermission permission,
            CancellationToken cancellationToken) =>
            Task.FromResult(items.SingleOrDefault(item => item.Id == workItemId && item.TenantId == tenantId));

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
            Task.FromResult<IReadOnlyList<WorkItem>>(
                items.Where(item => item.TenantId == tenantId && workItemIds.Contains(item.Id)).ToArray());
        public Task<bool> HasChildrenAsync(Guid tenantId, Guid parentWorkItemId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task RemoveAsync(WorkItem workItem, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
