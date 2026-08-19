using Orbit.Domain.Common;

namespace Orbit.Domain.WorkItems;

/// <summary>
/// Marks that a user wants to be notified of new comments on a work item (§10.5's "comment on a
/// watched item" trigger). One row per (work item, user); watching is add/remove, never edited.
/// </summary>
public sealed class WorkItemWatcher
{
    private WorkItemWatcher()
    {
    }

    private WorkItemWatcher(Guid id, Guid tenantId, Guid workItemId, Guid userId, DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        WorkItemId = workItemId;
        UserId = userId;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid WorkItemId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static WorkItemWatcher Create(Guid tenantId, Guid workItemId, Guid userId, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || workItemId == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainException("Tenant, work item, and user ids are required.");
        }

        return new WorkItemWatcher(Guid.CreateVersion7(), tenantId, workItemId, userId, now);
    }
}
