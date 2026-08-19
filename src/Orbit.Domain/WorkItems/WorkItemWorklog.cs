using Orbit.Domain.Common;

namespace Orbit.Domain.WorkItems;

/// <summary>
/// A tenant-scoped time-tracking entry ("Log work") against a work item.
/// </summary>
public sealed class WorkItemWorklog
{
    private WorkItemWorklog()
    {
    }

    private WorkItemWorklog(
        Guid id,
        Guid tenantId,
        Guid workItemId,
        Guid authorMembershipId,
        int minutesSpent,
        DateOnly workDate,
        string? description,
        DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        WorkItemId = workItemId;
        AuthorMembershipId = authorMembershipId;
        MinutesSpent = minutesSpent;
        WorkDate = workDate;
        Description = description;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid WorkItemId { get; private set; }
    public Guid AuthorMembershipId { get; private set; }
    public int MinutesSpent { get; private set; }
    public DateOnly WorkDate { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static WorkItemWorklog Create(
        Guid tenantId,
        Guid workItemId,
        Guid authorMembershipId,
        int minutesSpent,
        DateOnly workDate,
        string? description,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || workItemId == Guid.Empty || authorMembershipId == Guid.Empty)
        {
            throw new DomainException("Tenant, work item, and author ids are required.");
        }

        if (minutesSpent is < 1 or > 1440)
        {
            throw new DomainException("Time spent must be between 1 minute and 24 hours.");
        }

        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (normalizedDescription?.Length > 2_000)
        {
            throw new DomainException("Work log description cannot exceed 2,000 characters.");
        }

        return new WorkItemWorklog(
            Guid.CreateVersion7(), tenantId, workItemId, authorMembershipId, minutesSpent, workDate,
            normalizedDescription, now);
    }
}
