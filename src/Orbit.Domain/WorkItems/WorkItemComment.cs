using Orbit.Domain.Common;

namespace Orbit.Domain.WorkItems;

/// <summary>
/// A tenant-scoped comment on a work item. Comments are soft-deleted to preserve history
/// fidelity: the row is retained and the body is masked by the DTO layer after deletion.
/// </summary>
public sealed class WorkItemComment
{
    private WorkItemComment()
    {
    }

    private WorkItemComment(
        Guid id,
        Guid tenantId,
        Guid workItemId,
        Guid authorMembershipId,
        string body,
        Guid[] mentionedUserIds,
        DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        WorkItemId = workItemId;
        AuthorMembershipId = authorMembershipId;
        Body = body;
        MentionedUserIds = mentionedUserIds;
        Version = 1;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid WorkItemId { get; private set; }

    /// <summary>
    /// Membership id of the principal who wrote the comment. Used to enforce that only
    /// the author may edit or delete their own comment.
    /// </summary>
    public Guid AuthorMembershipId { get; private set; }

    public string Body { get; private set; } = string.Empty;

    /// <summary>
    /// User ids extracted from @{userId} tokens in the body at write time.
    /// Stored so the frontend can resolve display names without re-parsing body text.
    /// Notification delivery is handled by the E8.2 Notifications epic.
    /// </summary>
    public Guid[] MentionedUserIds { get; private set; } = [];

    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? LastEditedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt.HasValue;

    public static WorkItemComment Create(
        Guid tenantId,
        Guid workItemId,
        Guid authorMembershipId,
        string body,
        Guid[] mentionedUserIds,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || workItemId == Guid.Empty || authorMembershipId == Guid.Empty)
        {
            throw new DomainException("Tenant, work item, and author ids are required.");
        }

        return new WorkItemComment(
            Guid.CreateVersion7(),
            tenantId,
            workItemId,
            authorMembershipId,
            NormalizeBody(body),
            mentionedUserIds,
            now);
    }

    /// <summary>
    /// Replaces the body with new content. No-op if the trimmed body is identical.
    /// </summary>
    public void Edit(string body, DateTimeOffset now)
    {
        if (IsDeleted)
        {
            throw new DomainException("A deleted comment cannot be edited.");
        }

        var normalized = NormalizeBody(body);
        if (normalized == Body)
        {
            return;
        }

        Body = normalized;
        LastEditedAt = now;
        Version++;
        UpdatedAt = now;
    }

    /// <summary>
    /// Soft-deletes the comment. Idempotent: a second call is a no-op.
    /// </summary>
    public void Delete(DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return;
        }

        DeletedAt = now;
        Version++;
        UpdatedAt = now;
    }

    private static string NormalizeBody(string body)
    {
        var normalized = body?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 10_000)
        {
            throw new DomainException("Comment body must contain 1 to 10,000 characters.");
        }

        return normalized;
    }
}
