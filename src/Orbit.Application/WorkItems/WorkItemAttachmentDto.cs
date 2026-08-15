using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

public sealed record WorkItemAttachmentDto(
    Guid Id,
    Guid WorkItemId,
    string FileName,
    string ContentType,
    long SizeBytes,
    Guid UploadedByMembershipId,
    DateTimeOffset UploadedAt,
    string DownloadUrl)
{
    public static WorkItemAttachmentDto From(Attachment attachment, string downloadUrl) =>
        new(
            attachment.Id,
            attachment.WorkItemId,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.UploadedByMembershipId,
            attachment.UploadedAt,
            downloadUrl);
}

public sealed record PresignedAttachmentUploadDto(string UploadUrl, string ObjectKey, DateTimeOffset ExpiresAt);
