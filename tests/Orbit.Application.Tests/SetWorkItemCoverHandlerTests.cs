using FluentValidation;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.WorkItems;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.Tests;

public sealed class SetWorkItemCoverHandlerTests
{
    [Fact]
    public async Task Handle_ImageAttachment_SetsCover()
    {
        var tenantId = Guid.NewGuid();
        var workItem = WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Cover this card", null, WorkItemType.Task, Priority.Medium,
            DateTimeOffset.UtcNow);
        var attachment = Attachment.Create(
            tenantId, workItem.Id, "cover.png", "image/png", 1024, "object-key", Guid.NewGuid(), DateTimeOffset.UtcNow);
        attachment.MarkScanned(AttachmentScanStatus.Clean, DateTimeOffset.UtcNow);
        var handler = new SetWorkItemCoverHandler(
            new TenantContextStub(tenantId), new WorkItemRepositoryStub(workItem),
            new AttachmentRepositoryStub(attachment), new UnitOfWorkStub(), TimeProvider.System);

        var result = await handler.Handle(
            new SetWorkItemCoverCommand(workItem.Id, attachment.Id, workItem.Version), CancellationToken.None);

        Assert.Equal(attachment.Id, result.CoverAttachmentId);
    }

    [Fact]
    public async Task Handle_NonImageAttachment_ThrowsValidationException()
    {
        var tenantId = Guid.NewGuid();
        var workItem = WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Cover this card", null, WorkItemType.Task, Priority.Medium,
            DateTimeOffset.UtcNow);
        var attachment = Attachment.Create(
            tenantId, workItem.Id, "notes.pdf", "application/pdf", 1024, "object-key", Guid.NewGuid(),
            DateTimeOffset.UtcNow);
        var handler = new SetWorkItemCoverHandler(
            new TenantContextStub(tenantId), new WorkItemRepositoryStub(workItem),
            new AttachmentRepositoryStub(attachment), new UnitOfWorkStub(), TimeProvider.System);

        var action = () => handler.Handle(
            new SetWorkItemCoverCommand(workItem.Id, attachment.Id, workItem.Version), CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(action);
    }

    [Fact]
    public async Task Handle_UnscannedImageAttachment_ThrowsValidationException()
    {
        var tenantId = Guid.NewGuid();
        var workItem = WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Cover this card", null, WorkItemType.Task, Priority.Medium,
            DateTimeOffset.UtcNow);
        var attachment = Attachment.Create(
            tenantId, workItem.Id, "cover.png", "image/png", 1024, "object-key", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var handler = new SetWorkItemCoverHandler(
            new TenantContextStub(tenantId), new WorkItemRepositoryStub(workItem),
            new AttachmentRepositoryStub(attachment), new UnitOfWorkStub(), TimeProvider.System);

        var action = () => handler.Handle(
            new SetWorkItemCoverCommand(workItem.Id, attachment.Id, workItem.Version), CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(action);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

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

    private sealed class AttachmentRepositoryStub(Attachment attachment) : IAttachmentRepository
    {
        public Task AddAsync(Attachment value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Attachment?> GetAsync(
            Guid tenantId, Guid workItemId, Guid attachmentId, CancellationToken cancellationToken) =>
            Task.FromResult<Attachment?>(
                attachment.Id == attachmentId && attachment.TenantId == tenantId ? attachment : null);
        public Task<IReadOnlyList<Attachment>> ListByWorkItemAsync(
            Guid tenantId, Guid workItemId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Attachment>>([attachment]);
        public Task RemoveAsync(Attachment value, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
