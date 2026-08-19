using Orbit.Domain.Common;

namespace Orbit.Domain.WorkItems;

/// <summary>
/// Marks that a user has voted for a work item to signal priority interest. One row per
/// (work item, user); voting is add/remove, never edited.
/// </summary>
public sealed class WorkItemVote
{
    private WorkItemVote()
    {
    }

    private WorkItemVote(Guid id, Guid tenantId, Guid workItemId, Guid userId, DateTimeOffset now)
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

    public static WorkItemVote Create(Guid tenantId, Guid workItemId, Guid userId, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || workItemId == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainException("Tenant, work item, and user ids are required.");
        }

        return new WorkItemVote(Guid.CreateVersion7(), tenantId, workItemId, userId, now);
    }
}
