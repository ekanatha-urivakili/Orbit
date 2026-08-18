using FluentValidation;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.WorkItems;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.Tests;

public sealed class WorkItemAttachmentHandlerTests
{
    private static WorkItem NewItem(Guid tenantId, Guid projectId) =>
        WorkItem.Create(
            tenantId, projectId, 1, "ORB", "Card 1", null,
            WorkItemType.Task, Priority.Medium, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Presign_ReturnsUploadUrlScopedToTenantAndWorkItem()
    {
        var tenantId = Guid.NewGuid();
        var item = NewItem(tenantId, Guid.NewGuid());
        var storage = new ObjectStorageServiceStub();
        var handler = new PresignWorkItemAttachmentUploadHandler(
            new TenantContextStub(tenantId),
            new WorkItemRepositoryStub(item),
            storage);

        var result = await handler.Handle(
            new PresignWorkItemAttachmentUploadCommand(item.Id, "diagram.png", "image/png", 2048),
            CancellationToken.None);

        Assert.StartsWith($"{tenantId:N}/{item.Id:N}/", result.ObjectKey, StringComparison.Ordinal);
        Assert.EndsWith("diagram.png", result.ObjectKey, StringComparison.Ordinal);
        Assert.Equal("https://storage.test/upload", result.UploadUrl);
    }

    [Fact]
    public async Task Presign_HidesExistence_WhenWorkItemNotVisible()
    {
        var tenantId = Guid.NewGuid();
        var handler = new PresignWorkItemAttachmentUploadHandler(
            new TenantContextStub(tenantId),
            new WorkItemRepositoryStub(),
            new ObjectStorageServiceStub());

        var action = () => handler.Handle(
            new PresignWorkItemAttachmentUploadCommand(Guid.NewGuid(), "file.png", "image/png", 100),
            CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    [Fact]
    public async Task Confirm_CreatesAttachmentAndReturnsDownloadUrl()
    {
        var tenantId = Guid.NewGuid();
        var item = NewItem(tenantId, Guid.NewGuid());
        var attachments = new AttachmentRepositoryStub();
        var handler = new ConfirmWorkItemAttachmentHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new WorkItemRepositoryStub(item),
            attachments,
            new ObjectStorageServiceStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);
        var objectKey = $"{tenantId:N}/{item.Id:N}/abc-diagram.png";

        var result = await handler.Handle(
            new ConfirmWorkItemAttachmentCommand(item.Id, "diagram.png", "image/png", 2048, objectKey),
            CancellationToken.None);

        Assert.Equal("diagram.png", result.FileName);
        Assert.Equal("https://storage.test/download", result.DownloadUrl);
        Assert.Single(attachments.Added);
        Assert.Equal(objectKey, attachments.Added[0].ObjectKey);
    }

    [Fact]
    public async Task Confirm_RejectsObjectKeyNotBelongingToWorkItem()
    {
        var tenantId = Guid.NewGuid();
        var item = NewItem(tenantId, Guid.NewGuid());
        var handler = new ConfirmWorkItemAttachmentHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new WorkItemRepositoryStub(item),
            new AttachmentRepositoryStub(),
            new ObjectStorageServiceStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new ConfirmWorkItemAttachmentCommand(item.Id, "diagram.png", "image/png", 2048, "someone-elses/key.png"),
            CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(action);
    }

    [Fact]
    public async Task List_ReturnsAttachmentsWithFreshDownloadUrls()
    {
        var tenantId = Guid.NewGuid();
        var item = NewItem(tenantId, Guid.NewGuid());
        var attachment = Attachment.Create(
            tenantId, item.Id, "diagram.png", "image/png", 2048, "key", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var handler = new ListWorkItemAttachmentsHandler(
            new TenantContextStub(tenantId),
            new WorkItemRepositoryStub(item),
            new AttachmentRepositoryStub(attachment),
            new ObjectStorageServiceStub());

        var result = await handler.Handle(new ListWorkItemAttachmentsQuery(item.Id), CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal("https://storage.test/download", dto.DownloadUrl);
    }

    [Fact]
    public async Task Delete_RemovesOwnAttachmentFromStorageAndRepository()
    {
        var tenantId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();
        var uploaderMembershipId = Guid.NewGuid();
        var attachment = Attachment.Create(
            tenantId, workItemId, "diagram.png", "image/png", 2048, "key", uploaderMembershipId, DateTimeOffset.UtcNow);
        var attachments = new AttachmentRepositoryStub(attachment);
        var storage = new ObjectStorageServiceStub();
        var handler = new DeleteWorkItemAttachmentHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(uploaderMembershipId),
            attachments,
            storage,
            new UnitOfWorkStub());

        await handler.Handle(new DeleteWorkItemAttachmentCommand(workItemId, attachment.Id), CancellationToken.None);

        Assert.Contains(attachment.ObjectKey, storage.Deleted);
        Assert.Contains(attachment, attachments.Removed);
    }

    [Fact]
    public async Task Delete_HidesExistence_WhenCallerDidNotUploadIt()
    {
        var tenantId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();
        var attachment = Attachment.Create(
            tenantId, workItemId, "diagram.png", "image/png", 2048, "key", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var handler = new DeleteWorkItemAttachmentHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(Guid.NewGuid()),
            new AttachmentRepositoryStub(attachment),
            new ObjectStorageServiceStub(),
            new UnitOfWorkStub());

        var action = () => handler.Handle(
            new DeleteWorkItemAttachmentCommand(workItemId, attachment.Id), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    [Fact]
    public void PresignValidator_RejectsSvg()
    {
        var validator = new PresignWorkItemAttachmentUploadValidator();
        
        var validCommand = new PresignWorkItemAttachmentUploadCommand(Guid.NewGuid(), "test.png", "image/png", 2048);
        var invalidCommand = new PresignWorkItemAttachmentUploadCommand(Guid.NewGuid(), "test.svg", "image/svg+xml", 2048);
        
        var validResult = validator.Validate(validCommand);
        var invalidResult = validator.Validate(invalidCommand);
        
        Assert.True(validResult.IsValid);
        Assert.False(invalidResult.IsValid);
        Assert.Contains(invalidResult.Errors, error => error.PropertyName == nameof(PresignWorkItemAttachmentUploadCommand.ContentType));
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class CurrentPrincipalStub(Guid? membershipId = null) : ICurrentPrincipal
    {
        public Guid? UserId => Guid.NewGuid();
        public Guid? SessionId => null;
        public Guid MembershipId { get; } = membershipId ?? Guid.NewGuid();
        public PrincipalType PrincipalType => PrincipalType.User;
        public TenantRole TenantRole => TenantRole.Owner;
        public MembershipTier MembershipTier => MembershipTier.Standard;
        public bool IsDevelopmentBypass => true;
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
                items.Where(item => item.TenantId == tenantId && workItemIds.Contains(item.Id)).ToArray());
    }

    private sealed class AttachmentRepositoryStub(params Attachment[] attachments) : IAttachmentRepository
    {
        private readonly List<Attachment> _attachments = [.. attachments];
        public List<Attachment> Added { get; } = [];
        public List<Attachment> Removed { get; } = [];

        public Task AddAsync(Attachment attachment, CancellationToken cancellationToken)
        {
            Added.Add(attachment);
            _attachments.Add(attachment);
            return Task.CompletedTask;
        }

        public Task<Attachment?> GetAsync(
            Guid tenantId, Guid workItemId, Guid attachmentId, CancellationToken cancellationToken) =>
            Task.FromResult(_attachments.SingleOrDefault(attachment =>
                attachment.TenantId == tenantId && attachment.WorkItemId == workItemId && attachment.Id == attachmentId));

        public Task<IReadOnlyList<Attachment>> ListByWorkItemAsync(
            Guid tenantId, Guid workItemId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Attachment>>(
                [.. _attachments.Where(attachment => attachment.TenantId == tenantId && attachment.WorkItemId == workItemId)]);

        public Task RemoveAsync(Attachment attachment, CancellationToken cancellationToken)
        {
            Removed.Add(attachment);
            _attachments.Remove(attachment);
            return Task.CompletedTask;
        }
    }

    private sealed class ObjectStorageServiceStub : IObjectStorageService
    {
        public List<string> Deleted { get; } = [];

        public PresignedUpload CreatePresignedUpload(string objectKey, string contentType, TimeSpan expiresIn) =>
            new("https://storage.test/upload", objectKey, DateTimeOffset.UtcNow.Add(expiresIn));

        public string CreatePresignedDownloadUrl(string objectKey, TimeSpan expiresIn) => "https://storage.test/download";

        public string CreatePresignedDisplayUrl(string objectKey, TimeSpan expiresIn) => "https://storage.test/display";

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
        {
            Deleted.Add(objectKey);
            return Task.CompletedTask;
        }
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
