using Orbit.Domain.Choices;
using Orbit.Domain.Common;

namespace Orbit.Domain.Boards;

/// <summary>
/// An append-only audit row for a sprint-scope change (item added/removed, status/estimate changed,
/// sprint completed or reopened) - folded by <c>SprintReportHandler</c> into the burndown chart.
/// <see cref="WorkItemId"/> is null for sprint-level facts (<see cref="AgileFactType.SprintCompleted"/>,
/// <see cref="AgileFactType.SprintReopened"/>) that aren't about one specific item. <see cref="EstimateDelta"/>
/// is the signed change in remaining points this fact caused (e.g. +points when added to the sprint,
/// -points when the item is marked Done) so a burndown is a running sum over facts in <see cref="OccurredAt"/>
/// order; it is null for facts that don't move the burndown line (sprint-level markers, non-Done status moves).
/// </summary>
public sealed class SprintScopeFact
{
    private SprintScopeFact()
    {
    }

    private SprintScopeFact(
        Guid tenantId,
        Guid sprintId,
        Guid? workItemId,
        AgileFactType factType,
        decimal? estimateDelta,
        DateTimeOffset occurredAt,
        DateTimeOffset recordedAt)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        SprintId = sprintId;
        WorkItemId = workItemId;
        FactType = factType;
        EstimateDelta = estimateDelta;
        OccurredAt = occurredAt;
        RecordedAt = recordedAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SprintId { get; private set; }
    public Guid? WorkItemId { get; private set; }
    public AgileFactType FactType { get; private set; }
    public decimal? EstimateDelta { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    public static SprintScopeFact Create(
        Guid tenantId,
        Guid sprintId,
        Guid? workItemId,
        AgileFactType factType,
        decimal? estimateDelta,
        DateTimeOffset occurredAt,
        DateTimeOffset recordedAt)
    {
        if (tenantId == Guid.Empty || sprintId == Guid.Empty)
        {
            throw new DomainException("Tenant and sprint ids are required.");
        }

        return new SprintScopeFact(tenantId, sprintId, workItemId, factType, estimateDelta, occurredAt, recordedAt);
    }
}
