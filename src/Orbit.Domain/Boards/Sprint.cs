using Orbit.Domain.Choices;
using Orbit.Domain.Common;

namespace Orbit.Domain.Boards;

public sealed class Sprint
{
    private Sprint()
    {
    }

    private Sprint(Guid tenantId, Guid projectId, string name, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        ProjectId = projectId;
        Name = name;
        State = SprintState.Future;
        Version = 1;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Goal { get; private set; }
    public SprintState State { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Sprint Create(Guid tenantId, Guid projectId, string name, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty)
        {
            throw new DomainException("Tenant and project ids are required.");
        }

        return new Sprint(tenantId, projectId, NormalizeName(name), now);
    }

    public void Start(string? goal, DateOnly? startDate, DateOnly? endDate, DateTimeOffset now)
    {
        if (State != SprintState.Future)
        {
            throw new DomainException("Only a future sprint can be started.");
        }

        if (startDate is not null && endDate is not null && endDate < startDate)
        {
            throw new DomainException("A sprint's end date cannot be before its start date.");
        }

        Goal = NormalizeGoal(goal);
        StartDate = startDate ?? DateOnly.FromDateTime(now.UtcDateTime);
        EndDate = endDate;
        State = SprintState.Active;
        Version++;
        UpdatedAt = now;
    }

    public void StartClosing(DateTimeOffset now)
    {
        if (State is not (SprintState.Active or SprintState.Reopened))
        {
            throw new DomainException("Only an active or reopened sprint can be closed.");
        }

        State = SprintState.Closing;
        Version++;
        UpdatedAt = now;
    }

    public void FinishClosing(DateTimeOffset now)
    {
        if (State != SprintState.Closing)
        {
            throw new DomainException("Only a closing sprint can finish closing.");
        }

        EndDate ??= DateOnly.FromDateTime(now.UtcDateTime);
        State = SprintState.Closed;
        Version++;
        UpdatedAt = now;
    }

    public void Reopen(DateTimeOffset now)
    {
        if (State != SprintState.Closed)
        {
            throw new DomainException("Only a closed sprint can be reopened.");
        }

        State = SprintState.Reopened;
        Version++;
        UpdatedAt = now;
    }

    private static string NormalizeName(string name)
    {
        var normalized = name.Trim();
        if (normalized.Length is < 2 or > 120)
        {
            throw new DomainException("Sprint name must contain 2 to 120 characters.");
        }

        return normalized;
    }

    private static string? NormalizeGoal(string? goal)
    {
        var normalized = string.IsNullOrWhiteSpace(goal) ? null : goal.Trim();
        if (normalized?.Length > 2_000)
        {
            throw new DomainException("Sprint goal cannot exceed 2,000 characters.");
        }

        return normalized;
    }
}

public sealed class SprintMembership
{
    private SprintMembership()
    {
    }

    private SprintMembership(Guid tenantId, Guid sprintId, Guid workItemId, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        SprintId = sprintId;
        WorkItemId = workItemId;
        AddedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SprintId { get; private set; }
    public Guid WorkItemId { get; private set; }
    public DateTimeOffset AddedAt { get; private set; }
    public DateTimeOffset? RemovedAt { get; private set; }

    public static SprintMembership Create(Guid tenantId, Guid sprintId, Guid workItemId, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || sprintId == Guid.Empty || workItemId == Guid.Empty)
        {
            throw new DomainException("Tenant, sprint, and work item ids are required.");
        }

        return new SprintMembership(tenantId, sprintId, workItemId, now);
    }

    public void Remove(DateTimeOffset now)
    {
        if (RemovedAt is not null)
        {
            return;
        }

        RemovedAt = now;
    }
}
