using Orbit.Domain.Choices;
using Orbit.Domain.Common;

namespace Orbit.Domain.WorkItems;

/// <summary>
/// A directed relationship between two work items. <see cref="Kind"/> is always expressed from
/// <see cref="SourceWorkItemId"/>'s perspective (e.g. source Blocks target); the inverse label
/// (e.g. "is blocked by") is derived by callers when rendering the link on the target item.
/// </summary>
public sealed class WorkItemLink
{
    private WorkItemLink()
    {
    }

    private WorkItemLink(
        Guid id,
        Guid tenantId,
        Guid sourceWorkItemId,
        Guid targetWorkItemId,
        WorkItemLinkKind kind,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        SourceWorkItemId = sourceWorkItemId;
        TargetWorkItemId = targetWorkItemId;
        Kind = kind;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SourceWorkItemId { get; private set; }
    public Guid TargetWorkItemId { get; private set; }
    public WorkItemLinkKind Kind { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static WorkItemLink Create(
        Guid tenantId,
        Guid sourceWorkItemId,
        Guid targetWorkItemId,
        WorkItemLinkKind kind,
        DateTimeOffset now)
    {
        if (sourceWorkItemId == targetWorkItemId)
        {
            throw new DomainException("A work item cannot link to itself.");
        }

        return new WorkItemLink(Guid.CreateVersion7(), tenantId, sourceWorkItemId, targetWorkItemId, kind, now);
    }
}
