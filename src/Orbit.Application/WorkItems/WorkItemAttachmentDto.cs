using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

/// <param name="DownloadUrl">
/// Null until <see cref="ScanStatus"/> is <see cref="AttachmentScanStatus.Clean"/> — the file is not
/// downloadable while a scan is <c>Pending</c> or has flagged it <c>Infected</c>/<c>Failed</c>.
/// </param>
public sealed record WorkItemAttachmentDto(
    Guid Id,
    Guid WorkItemId,
    string FileName,
    string ContentType,
    long SizeBytes,
    Guid UploadedByMembershipId,
    DateTimeOffset UploadedAt,
    AttachmentScanStatus ScanStatus,
    string? DownloadUrl)
{
    public static WorkItemAttachmentDto From(Attachment attachment, string? downloadUrl) =>
        new(
            attachment.Id,
            attachment.WorkItemId,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.UploadedByMembershipId,
            attachment.UploadedAt,
            attachment.ScanStatus,
            attachment.ScanStatus == AttachmentScanStatus.Clean ? downloadUrl : null);
}

public sealed record PresignedAttachmentUploadDto(string UploadUrl, string ObjectKey, DateTimeOffset ExpiresAt);
