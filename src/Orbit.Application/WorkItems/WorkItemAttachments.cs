using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Messaging;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

/// <summary>
/// Content types accepted for work-item attachments. Deliberately narrow — an additional defense
/// alongside the worker-driven malware scan (see ARCH-ORBIT-001 §10.3), not a substitute for it.
/// </summary>
internal static class AttachmentContentTypes
{
    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        // SEC-05: image/svg+xml deliberately excluded — SVG is XML and can carry embedded
        // <script> tags / event handlers that execute when served directly in the browser
        // via a presigned URL. No server-side SVG sanitisation is in place (ARCH-ORBIT-001 §13.5.2).
        "image/png", "image/jpeg", "image/gif", "image/webp",
        "application/pdf", "text/plain", "text/csv", "application/json",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/zip",
    };

    public const long MaxSizeBytes = 25 * 1024 * 1024;
}

// ---------------------------------------------------------------------------
// Presign upload
// ---------------------------------------------------------------------------

public sealed record PresignWorkItemAttachmentUploadCommand(
    Guid WorkItemId,
    string FileName,
    string ContentType,
    long SizeBytes) : ICommand<PresignedAttachmentUploadDto>;

public sealed class PresignWorkItemAttachmentUploadValidator : AbstractValidator<PresignWorkItemAttachmentUploadCommand>
{
    public PresignWorkItemAttachmentUploadValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.FileName).NotEmpty().MaximumLength(255);
        RuleFor(command => command.ContentType)
            .Must(contentType => AttachmentContentTypes.Allowed.Contains(contentType))
            .WithMessage("This file type is not allowed for attachments.");
        RuleFor(command => command.SizeBytes).InclusiveBetween(1, AttachmentContentTypes.MaxSizeBytes);
    }
}

public sealed class PresignWorkItemAttachmentUploadHandler(
    ITenantContext tenantContext,
    IWorkItemRepository workItems,
    IObjectStorageService storage)
    : IRequestHandler<PresignWorkItemAttachmentUploadCommand, PresignedAttachmentUploadDto>
{
    public async Task<PresignedAttachmentUploadDto> Handle(
        PresignWorkItemAttachmentUploadCommand request,
        CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetAsync(
                tenantContext.TenantId, request.WorkItemId, ProjectPermission.TransitionWorkItem, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        var objectKey = $"{tenantContext.TenantId:N}/{workItem.Id:N}/{Guid.NewGuid():N}-{SanitizeFileName(request.FileName)}";
        var upload = storage.CreatePresignedUpload(objectKey, request.ContentType, TimeSpan.FromMinutes(15));
        return new PresignedAttachmentUploadDto(upload.UploadUrl, upload.ObjectKey, upload.ExpiresAt);
    }

    private static string SanitizeFileName(string fileName)
    {
        var safeChars = fileName.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_').ToArray();
        return new string(safeChars);
    }
}

// ---------------------------------------------------------------------------
// Confirm upload
// ---------------------------------------------------------------------------

public sealed record ConfirmWorkItemAttachmentCommand(
    Guid WorkItemId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string ObjectKey) : ICommand<WorkItemAttachmentDto>;

public sealed class ConfirmWorkItemAttachmentValidator : AbstractValidator<ConfirmWorkItemAttachmentCommand>
{
    public ConfirmWorkItemAttachmentValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.FileName).NotEmpty().MaximumLength(255);
        RuleFor(command => command.ContentType)
            .Must(contentType => AttachmentContentTypes.Allowed.Contains(contentType))
            .WithMessage("This file type is not allowed for attachments.");
        RuleFor(command => command.SizeBytes).InclusiveBetween(1, AttachmentContentTypes.MaxSizeBytes);
        RuleFor(command => command.ObjectKey).NotEmpty().MaximumLength(1024);
    }
}

public sealed class ConfirmWorkItemAttachmentHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IWorkItemRepository workItems,
    IAttachmentRepository attachments,
    IAttachmentScanRequestRepository scanRequests,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<ConfirmWorkItemAttachmentCommand, WorkItemAttachmentDto>
{
    public async Task<WorkItemAttachmentDto> Handle(
        ConfirmWorkItemAttachmentCommand request,
        CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetAsync(
                tenantContext.TenantId, request.WorkItemId, ProjectPermission.TransitionWorkItem, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        // The object key must be one this tenant/work item's presign step could have minted —
        // otherwise a caller could attach metadata pointing at an object it never uploaded.
        var expectedPrefix = $"{tenantContext.TenantId:N}/{workItem.Id:N}/";
        if (!request.ObjectKey.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            throw new ValidationException("The object key does not belong to this work item.");
        }

        var now = timeProvider.GetUtcNow();
        var attachment = Attachment.Create(
            tenantContext.TenantId,
            workItem.Id,
            request.FileName,
            request.ContentType,
            request.SizeBytes,
            request.ObjectKey,
            principal.MembershipId,
            now);

        await attachments.AddAsync(attachment, cancellationToken);

        // Attachment starts Pending: the file is already in MinIO (the client PUT it directly), but
        // it is not downloadable until AttachmentScanProcessor flips it to Clean.
        var scanRequest = AttachmentScanRequest.Create(
            tenantContext.TenantId, workItem.Id, attachment.Id, attachment.ObjectKey, now);
        await scanRequests.AddAsync(scanRequest, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return WorkItemAttachmentDto.From(attachment, downloadUrl: null);
    }
}

// ---------------------------------------------------------------------------
// List attachments
// ---------------------------------------------------------------------------

public sealed record ListWorkItemAttachmentsQuery(Guid WorkItemId) : IQuery<IReadOnlyList<WorkItemAttachmentDto>>;

public sealed class ListWorkItemAttachmentsValidator : AbstractValidator<ListWorkItemAttachmentsQuery>
{
    public ListWorkItemAttachmentsValidator() => RuleFor(query => query.WorkItemId).NotEmpty();
}

public sealed class ListWorkItemAttachmentsHandler(
    ITenantContext tenantContext,
    IWorkItemRepository workItems,
    IAttachmentRepository attachments,
    IObjectStorageService storage)
    : IRequestHandler<ListWorkItemAttachmentsQuery, IReadOnlyList<WorkItemAttachmentDto>>
{
    public async Task<IReadOnlyList<WorkItemAttachmentDto>> Handle(
        ListWorkItemAttachmentsQuery request,
        CancellationToken cancellationToken)
    {
        _ = await workItems.GetAsync(
                tenantContext.TenantId, request.WorkItemId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        var workItemAttachments = await attachments.ListByWorkItemAsync(
            tenantContext.TenantId, request.WorkItemId, cancellationToken);

        // Infected/Failed attachments are excluded outright rather than shown with a withheld
        // download URL - callers should not learn that a malicious file was ever uploaded.
        return workItemAttachments
            .Where(attachment => attachment.ScanStatus
                is AttachmentScanStatus.Pending or AttachmentScanStatus.Clean)
            .Select(attachment => WorkItemAttachmentDto.From(
                attachment,
                attachment.ScanStatus == AttachmentScanStatus.Clean
                    ? storage.CreatePresignedDownloadUrl(attachment.ObjectKey, TimeSpan.FromMinutes(15))
                    : null))
            .ToArray();
    }
}

// ---------------------------------------------------------------------------
// Download attachment
// ---------------------------------------------------------------------------

public sealed record GetWorkItemAttachmentDownloadUrlQuery(Guid WorkItemId, Guid AttachmentId)
    : IQuery<WorkItemAttachmentDto>;

public sealed class GetWorkItemAttachmentDownloadUrlValidator
    : AbstractValidator<GetWorkItemAttachmentDownloadUrlQuery>
{
    public GetWorkItemAttachmentDownloadUrlValidator()
    {
        RuleFor(query => query.WorkItemId).NotEmpty();
        RuleFor(query => query.AttachmentId).NotEmpty();
    }
}

public sealed class GetWorkItemAttachmentDownloadUrlHandler(
    ITenantContext tenantContext,
    IWorkItemRepository workItems,
    IAttachmentRepository attachments,
    IObjectStorageService storage)
    : IRequestHandler<GetWorkItemAttachmentDownloadUrlQuery, WorkItemAttachmentDto>
{
    public async Task<WorkItemAttachmentDto> Handle(
        GetWorkItemAttachmentDownloadUrlQuery request, CancellationToken cancellationToken)
    {
        _ = await workItems.GetAsync(
                tenantContext.TenantId, request.WorkItemId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        var attachment = await attachments.GetAsync(
                tenantContext.TenantId, request.WorkItemId, request.AttachmentId, cancellationToken)
            ?? throw new NotFoundException("Attachment was not found.");

        // Infected/Failed attachments existence-hide as 404, same as an attachment nobody uploaded -
        // callers should not learn a malicious file was ever attached here.
        if (attachment.ScanStatus is AttachmentScanStatus.Infected or AttachmentScanStatus.Failed)
        {
            throw new NotFoundException("Attachment was not found.");
        }

        if (attachment.ScanStatus == AttachmentScanStatus.Pending)
        {
            throw new ConflictException("This attachment is still being scanned for malware.");
        }

        var downloadUrl = storage.CreatePresignedDownloadUrl(attachment.ObjectKey, TimeSpan.FromMinutes(15));
        return WorkItemAttachmentDto.From(attachment, downloadUrl);
    }
}

// ---------------------------------------------------------------------------
// Delete attachment
// ---------------------------------------------------------------------------

public sealed record DeleteWorkItemAttachmentCommand(Guid WorkItemId, Guid AttachmentId) : ICommand<Unit>;

public sealed class DeleteWorkItemAttachmentValidator : AbstractValidator<DeleteWorkItemAttachmentCommand>
{
    public DeleteWorkItemAttachmentValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.AttachmentId).NotEmpty();
    }
}

public sealed class DeleteWorkItemAttachmentHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IAttachmentRepository attachments,
    IObjectStorageService storage,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteWorkItemAttachmentCommand, Unit>
{
    public async Task<Unit> Handle(DeleteWorkItemAttachmentCommand request, CancellationToken cancellationToken)
    {
        var attachment = await attachments.GetAsync(
                tenantContext.TenantId, request.WorkItemId, request.AttachmentId, cancellationToken)
            ?? throw new NotFoundException("Attachment was not found.");

        if (attachment.UploadedByMembershipId != principal.MembershipId)
        {
            // Return 404 to avoid leaking existence of another member's attachment.
            throw new NotFoundException("Attachment was not found.");
        }

        await storage.DeleteAsync(attachment.ObjectKey, cancellationToken);
        await attachments.RemoveAsync(attachment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
