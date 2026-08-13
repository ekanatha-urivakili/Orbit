using Orbit.Domain.Common;

namespace Orbit.Domain.Boards;

public enum SprintCompletionOperationState
{
    Pending,
    Completed
}

/// <summary>
/// Tracks a sprint-close attempt so completed requests are idempotent. Sprint completion is atomic;
/// interrupted attempts roll back and can be retried from the original sprint state.
/// </summary>
public sealed class SprintCompletionOperation
{
    private SprintCompletionOperation()
    {
    }

    private SprintCompletionOperation(
        Guid tenantId,
        Guid sprintId,
        Guid? rolloverTargetSprintId,
        int totalCount,
        DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        SprintId = sprintId;
        RolloverTargetSprintId = rolloverTargetSprintId;
        State = SprintCompletionOperationState.Pending;
        ProcessedCount = 0;
        TotalCount = totalCount;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SprintId { get; private set; }
    public Guid? RolloverTargetSprintId { get; private set; }
    public SprintCompletionOperationState State { get; private set; }
    public int ProcessedCount { get; private set; }
    public int TotalCount { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static SprintCompletionOperation Create(
        Guid tenantId,
        Guid sprintId,
        Guid? rolloverTargetSprintId,
        int totalCount,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || sprintId == Guid.Empty)
        {
            throw new DomainException("Tenant and sprint ids are required.");
        }

        if (totalCount < 0)
        {
            throw new DomainException("Total count cannot be negative.");
        }

        return new SprintCompletionOperation(tenantId, sprintId, rolloverTargetSprintId, totalCount, now);
    }

    public void RecordProgress(int processedCount, DateTimeOffset now)
    {
        if (processedCount < ProcessedCount)
        {
            throw new DomainException("Processed count cannot move backward.");
        }

        ProcessedCount = processedCount;
        UpdatedAt = now;
    }

    public void MarkCompleted(DateTimeOffset now)
    {
        State = SprintCompletionOperationState.Completed;
        UpdatedAt = now;
    }
}
