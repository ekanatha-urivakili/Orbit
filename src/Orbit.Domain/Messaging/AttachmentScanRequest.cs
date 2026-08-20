using Orbit.Domain.Common;

namespace Orbit.Domain.Messaging;

/// <summary>
/// A transactional-outbox row asking the worker to malware-scan one newly-confirmed attachment.
/// Written in the same <c>SaveChangesAsync</c> call as <c>Attachment.Create</c>
/// (<c>ConfirmWorkItemAttachmentCommand</c>), so the two commit atomically; <see cref="Orbit.Worker"/>
/// polls and processes it out of band via <c>AttachmentScanProcessor</c>. Global, not tenant-scoped —
/// same rationale as <see cref="OutboxEmailMessage"/> (ADR-014): the worker has no ambient tenant
/// context, so it sets <c>app.tenant_id</c> itself from <see cref="TenantId"/> before touching the
/// referenced attachment row.
/// </summary>
public sealed class AttachmentScanRequest
{
    private AttachmentScanRequest()
    {
    }

    private AttachmentScanRequest(
        Guid id,
        Guid tenantId,
        Guid workItemId,
        Guid attachmentId,
        string objectKey,
        DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        WorkItemId = workItemId;
        AttachmentId = attachmentId;
        ObjectKey = objectKey;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid WorkItemId { get; private set; }
    public Guid AttachmentId { get; private set; }
    public string ObjectKey { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }

    public static AttachmentScanRequest Create(
        Guid tenantId, Guid workItemId, Guid attachmentId, string objectKey, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || workItemId == Guid.Empty || attachmentId == Guid.Empty)
        {
            throw new DomainException("Tenant, work item, and attachment ids are required.");
        }

        if (string.IsNullOrWhiteSpace(objectKey) || objectKey.Length > 1024)
        {
            throw new DomainException("Object key is required and must not exceed 1,024 characters.");
        }

        return new AttachmentScanRequest(Guid.CreateVersion7(), tenantId, workItemId, attachmentId, objectKey, now);
    }

    public void MarkProcessed(DateTimeOffset now)
    {
        ProcessedAt = now;
    }

    public void RecordFailure(string error)
    {
        Attempts++;
        LastError = error.Length > 2048 ? error[..2048] : error;
    }
}
