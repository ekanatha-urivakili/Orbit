using Orbit.Domain.Common;

namespace Orbit.Domain.WorkItems;

/// <summary>
/// Malware-scan lifecycle for an uploaded attachment. An attachment is not downloadable
/// (<see cref="Application.WorkItems.WorkItemAttachmentDto.DownloadUrl"/> is withheld) until it
/// reaches <see cref="Clean"/> — see <c>AttachmentScanProcessor</c> for the worker that transitions
/// it out of <see cref="Pending"/>.
/// </summary>
public enum AttachmentScanStatus
{
    Pending,
    Clean,
    Infected,
    Failed,
}

/// <summary>
/// Metadata for a file uploaded to object storage (MinIO locally, S3-compatible in production) and
/// linked to a work item. The binary payload never passes through the API process: the client PUTs
/// directly to <see cref="ObjectKey"/> using a presigned URL, and this row is created only after that
/// upload succeeds (see <c>ConfirmWorkItemAttachmentCommand</c>). Immutable once created except for
/// <see cref="ScanStatus"/> — an attachment can only be removed, never edited, matching Jira's
/// attachment model.
/// </summary>
public sealed class Attachment
{
    private Attachment()
    {
    }

    private Attachment(
        Guid id,
        Guid tenantId,
        Guid workItemId,
        string fileName,
        string contentType,
        long sizeBytes,
        string objectKey,
        Guid uploadedByMembershipId,
        DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        WorkItemId = workItemId;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        ObjectKey = objectKey;
        UploadedByMembershipId = uploadedByMembershipId;
        UploadedAt = now;
        ScanStatus = AttachmentScanStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid WorkItemId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string ObjectKey { get; private set; } = string.Empty;
    public Guid UploadedByMembershipId { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public AttachmentScanStatus ScanStatus { get; private set; } = AttachmentScanStatus.Pending;
    public DateTimeOffset? ScannedAt { get; private set; }

    public static Attachment Create(
        Guid tenantId,
        Guid workItemId,
        string fileName,
        string contentType,
        long sizeBytes,
        string objectKey,
        Guid uploadedByMembershipId,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || workItemId == Guid.Empty || uploadedByMembershipId == Guid.Empty)
        {
            throw new DomainException("Tenant, work item, and uploader ids are required.");
        }

        var normalizedFileName = fileName?.Trim() ?? string.Empty;
        if (normalizedFileName.Length is < 1 or > 255)
        {
            throw new DomainException("Attachment file name must contain 1 to 255 characters.");
        }

        var normalizedContentType = contentType?.Trim() ?? string.Empty;
        if (normalizedContentType.Length is < 1 or > 255)
        {
            throw new DomainException("Attachment content type must contain 1 to 255 characters.");
        }

        if (sizeBytes <= 0 || sizeBytes > 25 * 1024 * 1024)
        {
            throw new DomainException("Attachment size must be between 1 byte and 25 MB.");
        }

        if (string.IsNullOrWhiteSpace(objectKey) || objectKey.Length > 1024)
        {
            throw new DomainException("Attachment object key is required and must not exceed 1,024 characters.");
        }

        return new Attachment(
            Guid.CreateVersion7(),
            tenantId,
            workItemId,
            normalizedFileName,
            normalizedContentType,
            sizeBytes,
            objectKey,
            uploadedByMembershipId,
            now);
    }

    /// <summary>
    /// Records the outcome of the malware scan (<c>AttachmentScanProcessor</c>). Only a
    /// <see cref="Pending"/> attachment can be scanned — the scan result is final, matching the
    /// immutable-once-created model above.
    /// </summary>
    public void MarkScanned(AttachmentScanStatus status, DateTimeOffset now)
    {
        if (ScanStatus != AttachmentScanStatus.Pending)
        {
            throw new DomainException("Attachment has already been scanned.");
        }

        if (status == AttachmentScanStatus.Pending)
        {
            throw new DomainException("A scan result cannot leave the attachment Pending.");
        }

        ScanStatus = status;
        ScannedAt = now;
    }
}
