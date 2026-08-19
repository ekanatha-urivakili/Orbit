using FluentValidation;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.WorkItems;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.Tests;

public sealed class DeleteWorkItemHandlerTests
{
    [Fact]
    public async Task Handle_NoChildren_RemovesWorkItem()
    {
        var tenantId = Guid.NewGuid();
        var workItem = WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Delete this card", null, WorkItemType.Task, Priority.Medium,
            DateTimeOffset.UtcNow);
        var repository = new WorkItemRepositoryStub(workItem, hasChildren: false);
        var handler = new DeleteWorkItemHandler(new TenantContextStub(tenantId), repository, new UnitOfWorkStub());

        await handler.Handle(new DeleteWorkItemCommand(workItem.Id, workItem.Version), CancellationToken.None);

        Assert.True(repository.Removed);
    }

    [Fact]
    public async Task Handle_HasChildren_ThrowsValidationException()
    {
        var tenantId = Guid.NewGuid();
        var workItem = WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Parent card", null, WorkItemType.Story, Priority.Medium,
            DateTimeOffset.UtcNow);
        var repository = new WorkItemRepositoryStub(workItem, hasChildren: true);
        var handler = new DeleteWorkItemHandler(new TenantContextStub(tenantId), repository, new UnitOfWorkStub());

        var action = () => handler.Handle(
            new DeleteWorkItemCommand(workItem.Id, workItem.Version), CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(action);
        Assert.False(repository.Removed);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class WorkItemRepositoryStub(WorkItem workItem, bool hasChildren) : IWorkItemRepository
    {
        public bool Removed { get; private set; }
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
            Task.FromResult(hasChildren);
        public Task RemoveAsync(WorkItem workItem, CancellationToken cancellationToken)
        {
            Removed = true;
            return Task.CompletedTask;
        }
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
