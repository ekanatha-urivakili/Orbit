using FluentValidation;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.WorkItems;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.Tests;

public sealed class WorkItemLinkHandlerTests
{
    private static WorkItem NewItem(Guid tenantId, Guid projectId, long sequenceNumber = 1) =>
        WorkItem.Create(
            tenantId, projectId, sequenceNumber, "ORB", $"Card {sequenceNumber}", null,
            WorkItemType.Task, Priority.Medium, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Add_CreatesOutgoingLink_WhenNotInverse()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var source = NewItem(tenantId, projectId, 1);
        var target = NewItem(tenantId, projectId, 2);
        var links = new WorkItemLinkRepositoryStub();
        var handler = new AddWorkItemLinkHandler(
            new TenantContextStub(tenantId),
            new WorkItemRepositoryStub(source, target),
            links,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new AddWorkItemLinkCommand(source.Id, WorkItemLinkKind.Blocks, target.Id, Inverse: false),
            CancellationToken.None);

        Assert.Equal(WorkItemLinkDirection.Outgoing, result.Direction);
        Assert.Equal(target.Id, result.WorkItemId);
        var added = Assert.Single(links.Added);
        Assert.Equal(source.Id, added.SourceWorkItemId);
        Assert.Equal(target.Id, added.TargetWorkItemId);
    }

    [Fact]
    public async Task Add_CreatesIncomingLink_WhenInverse()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var item = NewItem(tenantId, projectId, 1);
        var other = NewItem(tenantId, projectId, 2);
        var links = new WorkItemLinkRepositoryStub();
        var handler = new AddWorkItemLinkHandler(
            new TenantContextStub(tenantId),
            new WorkItemRepositoryStub(item, other),
            links,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new AddWorkItemLinkCommand(item.Id, WorkItemLinkKind.Blocks, other.Id, Inverse: true),
            CancellationToken.None);

        Assert.Equal(WorkItemLinkDirection.Incoming, result.Direction);
        var added = Assert.Single(links.Added);
        Assert.Equal(other.Id, added.SourceWorkItemId);
        Assert.Equal(item.Id, added.TargetWorkItemId);
    }

    [Fact]
    public async Task Add_RejectsDuplicateRelationship()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var source = NewItem(tenantId, projectId, 1);
        var target = NewItem(tenantId, projectId, 2);
        var existing = WorkItemLink.Create(tenantId, source.Id, target.Id, WorkItemLinkKind.Blocks, DateTimeOffset.UtcNow);
        var handler = new AddWorkItemLinkHandler(
            new TenantContextStub(tenantId),
            new WorkItemRepositoryStub(source, target),
            new WorkItemLinkRepositoryStub(existing),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new AddWorkItemLinkCommand(source.Id, WorkItemLinkKind.Blocks, target.Id, Inverse: false),
            CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(action);
    }

    [Fact]
    public async Task List_ReturnsBothOutgoingAndIncomingLinks()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var item = NewItem(tenantId, projectId, 1);
        var blocked = NewItem(tenantId, projectId, 2);
        var blocker = NewItem(tenantId, projectId, 3);
        var outgoing = WorkItemLink.Create(tenantId, item.Id, blocked.Id, WorkItemLinkKind.Blocks, DateTimeOffset.UtcNow);
        var incoming = WorkItemLink.Create(tenantId, blocker.Id, item.Id, WorkItemLinkKind.Blocks, DateTimeOffset.UtcNow);
        var handler = new ListWorkItemLinksHandler(
            new TenantContextStub(tenantId),
            new WorkItemRepositoryStub(item, blocked, blocker),
            new WorkItemLinkRepositoryStub(outgoing, incoming));

        var result = await handler.Handle(new ListWorkItemLinksQuery(item.Id), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, dto => dto.WorkItemId == blocked.Id && dto.Direction == WorkItemLinkDirection.Outgoing);
        Assert.Contains(result, dto => dto.WorkItemId == blocker.Id && dto.Direction == WorkItemLinkDirection.Incoming);
    }

    [Fact]
    public async Task Remove_DeletesLink_WhenWorkItemParticipates()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var source = NewItem(tenantId, projectId, 1);
        var target = NewItem(tenantId, projectId, 2);
        var link = WorkItemLink.Create(tenantId, source.Id, target.Id, WorkItemLinkKind.RelatesTo, DateTimeOffset.UtcNow);
        var links = new WorkItemLinkRepositoryStub(link);
        var handler = new RemoveWorkItemLinkHandler(
            new TenantContextStub(tenantId),
            new WorkItemRepositoryStub(source, target),
            links,
            new UnitOfWorkStub());

        await handler.Handle(new RemoveWorkItemLinkCommand(source.Id, link.Id), CancellationToken.None);

        Assert.Contains(link, links.Removed);
    }

    [Fact]
    public async Task Remove_HidesExistence_WhenLinkDoesNotBelongToWorkItem()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var source = NewItem(tenantId, projectId, 1);
        var target = NewItem(tenantId, projectId, 2);
        var unrelated = NewItem(tenantId, projectId, 3);
        var link = WorkItemLink.Create(tenantId, source.Id, target.Id, WorkItemLinkKind.RelatesTo, DateTimeOffset.UtcNow);
        var handler = new RemoveWorkItemLinkHandler(
            new TenantContextStub(tenantId),
            new WorkItemRepositoryStub(source, target, unrelated),
            new WorkItemLinkRepositoryStub(link),
            new UnitOfWorkStub());

        var action = () => handler.Handle(new RemoveWorkItemLinkCommand(unrelated.Id, link.Id), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

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
                items.Where(item => item.TenantId == tenantId && workItemIds.Contains(item.Id)).ToArray());
    }

    private sealed class WorkItemLinkRepositoryStub(params WorkItemLink[] links) : IWorkItemLinkRepository
    {
        private readonly List<WorkItemLink> _links = [.. links];
        public List<WorkItemLink> Added { get; } = [];
        public List<WorkItemLink> Removed { get; } = [];

        public Task AddAsync(WorkItemLink link, CancellationToken cancellationToken)
        {
            Added.Add(link);
            _links.Add(link);
            return Task.CompletedTask;
        }

        public Task<WorkItemLink?> GetAsync(Guid tenantId, Guid linkId, CancellationToken cancellationToken) =>
            Task.FromResult(_links.SingleOrDefault(link => link.TenantId == tenantId && link.Id == linkId));

        public Task<IReadOnlyList<WorkItemLink>> ListByWorkItemAsync(
            Guid tenantId, Guid workItemId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItemLink>>(
                [.. _links.Where(link => link.TenantId == tenantId
                    && (link.SourceWorkItemId == workItemId || link.TargetWorkItemId == workItemId))]);

        public Task<bool> ExistsAsync(
            Guid tenantId, Guid sourceWorkItemId, Guid targetWorkItemId, WorkItemLinkKind kind,
            CancellationToken cancellationToken) =>
            Task.FromResult(_links.Any(link => link.TenantId == tenantId
                && link.SourceWorkItemId == sourceWorkItemId
                && link.TargetWorkItemId == targetWorkItemId
                && link.Kind == kind));

        public Task RemoveAsync(WorkItemLink link, CancellationToken cancellationToken)
        {
            Removed.Add(link);
            _links.Remove(link);
            return Task.CompletedTask;
        }
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
