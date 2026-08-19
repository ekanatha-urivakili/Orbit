using Orbit.Domain.Common;

namespace Orbit.Domain.WorkItems;

/// <summary>
/// An immutable, append-only record of a single field change on a work item — the Jira-style
/// "History" table. Entries are never edited or deleted, only created.
/// </summary>
public sealed class WorkItemHistoryEntry
{
    private WorkItemHistoryEntry()
    {
    }

    private WorkItemHistoryEntry(
        Guid id,
        Guid tenantId,
        Guid workItemId,
        Guid changedByMembershipId,
        string fieldName,
        string? oldValue,
        string? newValue,
        DateTimeOffset changedAt)
    {
        Id = id;
        TenantId = tenantId;
        WorkItemId = workItemId;
        ChangedByMembershipId = changedByMembershipId;
        FieldName = fieldName;
        OldValue = oldValue;
        NewValue = newValue;
        ChangedAt = changedAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid WorkItemId { get; private set; }
    public Guid ChangedByMembershipId { get; private set; }
    public string FieldName { get; private set; } = string.Empty;
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }

    public static WorkItemHistoryEntry Create(
        Guid tenantId,
        Guid workItemId,
        Guid changedByMembershipId,
        string fieldName,
        string? oldValue,
        string? newValue,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || workItemId == Guid.Empty || changedByMembershipId == Guid.Empty)
        {
            throw new DomainException("Tenant, work item, and changed-by ids are required.");
        }

        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new DomainException("Field name is required.");
        }

        return new WorkItemHistoryEntry(
            Guid.CreateVersion7(),
            tenantId,
            workItemId,
            changedByMembershipId,
            fieldName,
            oldValue,
            newValue,
            now);
    }
}
