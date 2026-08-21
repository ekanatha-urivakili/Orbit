using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Choices;
using Orbit.Domain.Configuration;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.Boards;

// §13.7 next-increment: cumulative flow diagram, control chart, and cycle-time reports, extending
// the existing SprintReportHandler burndown/velocity report (SprintReport.cs). All three fold the
// same two append-only, reproducible sources - SprintScopeFact (§13.5, per-item sprint membership
// intervals) and the "Status" WorkItemHistoryEntry rows ChangeWorkItemStatusHandler already writes
// unconditionally on every transition - rather than reading current WorkItem state, so a closed
// sprint's report stays reproducible even if items are later edited (NFR-14, matching burndown).
// History entries record the status *key* (immutable even if a status is later renamed via the
// workflow editor), so folding is stable across renames.

public sealed record CumulativeFlowStatusCountDto(Guid StatusId, string StatusName, int Count);

public sealed record CumulativeFlowPointDto(DateOnly Date, IReadOnlyList<CumulativeFlowStatusCountDto> StatusCounts);

public sealed record CumulativeFlowDiagramDto(Guid SprintId, IReadOnlyList<CumulativeFlowPointDto> Points);

public sealed record CumulativeFlowDiagramQuery(Guid SprintId) : IQuery<CumulativeFlowDiagramDto>;

public sealed class CumulativeFlowDiagramValidator : AbstractValidator<CumulativeFlowDiagramQuery>
{
    public CumulativeFlowDiagramValidator() => RuleFor(query => query.SprintId).NotEmpty();
}

public sealed record CompletedItemDto(Guid WorkItemId, DateTimeOffset StartedAt, DateTimeOffset CompletedAt, decimal CycleTimeDays);

public sealed record CycleTimeReportDto(
    Guid SprintId,
    IReadOnlyList<CompletedItemDto> Items,
    decimal? AverageCycleTimeDays,
    decimal? MedianCycleTimeDays);

public sealed record CycleTimeReportQuery(Guid SprintId) : IQuery<CycleTimeReportDto>;

public sealed class CycleTimeReportValidator : AbstractValidator<CycleTimeReportQuery>
{
    public CycleTimeReportValidator() => RuleFor(query => query.SprintId).NotEmpty();
}

public sealed record ControlChartDto(
    Guid SprintId,
    IReadOnlyList<CompletedItemDto> Points,
    decimal? AverageCycleTimeDays,
    decimal? P85CycleTimeDays);

public sealed record ControlChartQuery(Guid SprintId) : IQuery<ControlChartDto>;

public sealed class ControlChartValidator : AbstractValidator<ControlChartQuery>
{
    public ControlChartValidator() => RuleFor(query => query.SprintId).NotEmpty();
}

/// <summary>
/// Shared data access + folding for all three reports: loads the sprint, verifies project View
/// permission, loads sprint-scope facts, status-history entries, and the project's status catalog
/// for every item ever in the sprint, and folds them into per-item status timelines. See the file
/// header for why history (not current <see cref="WorkItem"/> state) is the read model.
/// </summary>
internal static class AgileReportData
{
    public static async Task<(
        Sprint Sprint,
        IReadOnlyList<SprintScopeFact> Facts,
        IReadOnlyDictionary<Guid, StatusTimeline> Timelines,
        IReadOnlyList<WorkItemStatusDefinition> Statuses)>
        LoadAsync(
            Guid sprintId,
            ITenantContext tenant,
            IProjectRepository projects,
            ISprintRepository sprints,
            ISprintScopeFactRepository sprintScopeFacts,
            IWorkItemHistoryRepository history,
            IWorkItemStatusRepository workItemStatuses,
            CancellationToken cancellationToken)
    {
        var sprint = await sprints.GetAsync(tenant.TenantId, sprintId, cancellationToken)
            ?? throw new NotFoundException("Sprint was not found.");
        _ = await projects.GetAsync(tenant.TenantId, sprint.ProjectId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Sprint was not found.");

        var statuses = await workItemStatuses.ListByProjectAsync(tenant.TenantId, sprint.ProjectId, cancellationToken);
        var defaultStatus = statuses.OrderBy(status => status.Order).FirstOrDefault();
        var statusesByKey = statuses.ToDictionary(status => status.Key);

        var facts = await sprintScopeFacts.ListBySprintAsync(tenant.TenantId, sprint.Id, cancellationToken);
        var workItemIds = facts
            .Where(fact => fact.WorkItemId.HasValue)
            .Select(fact => fact.WorkItemId!.Value)
            .Distinct()
            .ToArray();

        var statusEntries = await history.ListByWorkItemsAndFieldAsync(
            tenant.TenantId, workItemIds, "Status", cancellationToken);

        var timelines = workItemIds.ToDictionary(
            id => id,
            id => new StatusTimeline(
                statusEntries.Where(entry => entry.WorkItemId == id).ToArray(), statusesByKey, defaultStatus));

        return (sprint, facts, timelines, statuses);
    }

    /// <summary>
    /// Every membership interval ([AddedAt, RemovedAt)) a work item held in the sprint, derived
    /// from <see cref="AgileFactType.SprintAdded"/>/<see cref="AgileFactType.SprintRemoved"/>
    /// facts. An item added and later removed (or re-added) yields more than one interval.
    /// </summary>
    public static IReadOnlyDictionary<Guid, List<(DateTimeOffset Start, DateTimeOffset? End)>> BuildMembershipIntervals(
        IReadOnlyList<SprintScopeFact> facts)
    {
        var intervals = new Dictionary<Guid, List<(DateTimeOffset Start, DateTimeOffset? End)>>();
        foreach (var fact in facts
            .Where(fact => fact.WorkItemId.HasValue
                && fact.FactType is AgileFactType.SprintAdded or AgileFactType.SprintRemoved)
            .OrderBy(fact => fact.OccurredAt))
        {
            var list = intervals.TryGetValue(fact.WorkItemId!.Value, out var existing)
                ? existing
                : intervals[fact.WorkItemId.Value] = [];

            if (fact.FactType == AgileFactType.SprintAdded)
            {
                list.Add((fact.OccurredAt, null));
            }
            else if (list.Count > 0 && list[^1].End is null)
            {
                list[^1] = (list[^1].Start, fact.OccurredAt);
            }
        }

        return intervals;
    }

    public static bool IsInSprintAt(
        List<(DateTimeOffset Start, DateTimeOffset? End)> intervals, DateTimeOffset instant) =>
        intervals.Any(interval => interval.Start <= instant && (interval.End is null || instant < interval.End));
}

/// <summary>
/// A work item's status at any point in time, derived from its "Status" history entries (which
/// record the status <em>key</em>, not the display name, so a later rename doesn't break folding).
/// Before its first recorded transition a work item is assumed to hold the project's default
/// (lowest-order) status, matching the status every item is created with (§13.5.2).
/// </summary>
internal sealed class StatusTimeline
{
    private readonly IReadOnlyList<WorkItemHistoryEntry> _orderedEntries;
    private readonly IReadOnlyDictionary<string, WorkItemStatusDefinition> _statusesByKey;
    private readonly WorkItemStatusDefinition? _defaultStatus;

    public StatusTimeline(
        IReadOnlyList<WorkItemHistoryEntry> entries,
        IReadOnlyDictionary<string, WorkItemStatusDefinition> statusesByKey,
        WorkItemStatusDefinition? defaultStatus)
    {
        _orderedEntries = entries.OrderBy(entry => entry.ChangedAt).ToArray();
        _statusesByKey = statusesByKey;
        _defaultStatus = defaultStatus;
    }

    public WorkItemStatusDefinition? StatusAt(DateTimeOffset instant)
    {
        var status = _defaultStatus;
        foreach (var entry in _orderedEntries)
        {
            if (entry.ChangedAt > instant)
            {
                break;
            }

            if (entry.NewValue is not null && _statusesByKey.TryGetValue(entry.NewValue, out var resolved))
            {
                status = resolved;
            }
        }

        return status;
    }

    /// <summary>
    /// The (StartedAt, CompletedAt) pair for cycle time: the first transition into a
    /// <see cref="StatusCategory.InProgress"/> status before the last transition into a
    /// <see cref="StatusCategory.Done"/> status. Returns null when the item never reached a Done
    /// status, or reached Done without ever recording an InProgress transition (cycle time undefined).
    /// </summary>
    public (DateTimeOffset StartedAt, DateTimeOffset CompletedAt)? CycleTime()
    {
        var lastCompletedAt = _orderedEntries
            .Where(entry => entry.NewValue is not null
                && _statusesByKey.TryGetValue(entry.NewValue, out var status)
                && status.Category == StatusCategory.Done)
            .Select(entry => (DateTimeOffset?)entry.ChangedAt)
            .LastOrDefault();
        if (lastCompletedAt is not { } completedAt)
        {
            return null;
        }

        var startedAt = _orderedEntries
            .Where(entry => entry.ChangedAt < completedAt
                && entry.NewValue is not null
                && _statusesByKey.TryGetValue(entry.NewValue, out var status)
                && status.Category == StatusCategory.InProgress)
            .Select(entry => (DateTimeOffset?)entry.ChangedAt)
            .FirstOrDefault();
        if (startedAt is not { } started)
        {
            return null;
        }

        return (started, completedAt);
    }
}

public sealed class CumulativeFlowDiagramHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    ISprintRepository sprints,
    ISprintScopeFactRepository sprintScopeFacts,
    IWorkItemHistoryRepository history,
    IWorkItemStatusRepository workItemStatuses) : IRequestHandler<CumulativeFlowDiagramQuery, CumulativeFlowDiagramDto>
{
    public async Task<CumulativeFlowDiagramDto> Handle(
        CumulativeFlowDiagramQuery request, CancellationToken cancellationToken)
    {
        var (sprint, facts, timelines, statuses) = await AgileReportData.LoadAsync(
            request.SprintId, tenant, projects, sprints, sprintScopeFacts, history, workItemStatuses, cancellationToken);

        if (sprint.StartDate is not { } start)
        {
            return new CumulativeFlowDiagramDto(sprint.Id, []);
        }

        var end = sprint.EndDate ?? DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        if (end < start)
        {
            end = start;
        }
        else if (end.DayNumber - start.DayNumber > 366)
        {
            end = start.AddDays(366);
        }

        var membershipIntervals = AgileReportData.BuildMembershipIntervals(facts);

        var points = new List<CumulativeFlowPointDto>();
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            var dayEnd = new DateTimeOffset(day.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            var counts = statuses.ToDictionary(status => status.Id, _ => 0);

            foreach (var (workItemId, intervals) in membershipIntervals)
            {
                if (!AgileReportData.IsInSprintAt(intervals, dayEnd))
                {
                    continue;
                }

                var status = timelines[workItemId].StatusAt(dayEnd);
                if (status is not null && counts.ContainsKey(status.Id))
                {
                    counts[status.Id]++;
                }
            }

            points.Add(new CumulativeFlowPointDto(
                day,
                statuses.Select(status => new CumulativeFlowStatusCountDto(status.Id, status.Name, counts[status.Id])).ToArray()));
        }

        return new CumulativeFlowDiagramDto(sprint.Id, points);
    }
}

public sealed class CycleTimeReportHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    ISprintRepository sprints,
    ISprintScopeFactRepository sprintScopeFacts,
    IWorkItemHistoryRepository history,
    IWorkItemStatusRepository workItemStatuses) : IRequestHandler<CycleTimeReportQuery, CycleTimeReportDto>
{
    public async Task<CycleTimeReportDto> Handle(CycleTimeReportQuery request, CancellationToken cancellationToken)
    {
        var (sprint, _, timelines, _) = await AgileReportData.LoadAsync(
            request.SprintId, tenant, projects, sprints, sprintScopeFacts, history, workItemStatuses, cancellationToken);

        var items = BuildCompletedItems(timelines);
        var cycleTimeDays = items.Select(item => item.CycleTimeDays).OrderBy(days => days).ToArray();

        return new CycleTimeReportDto(
            sprint.Id,
            items,
            cycleTimeDays.Length == 0 ? null : cycleTimeDays.Average(),
            cycleTimeDays.Length == 0 ? null : Median(cycleTimeDays));
    }

    internal static IReadOnlyList<CompletedItemDto> BuildCompletedItems(
        IReadOnlyDictionary<Guid, StatusTimeline> timelines)
    {
        var items = new List<CompletedItemDto>();
        foreach (var (workItemId, timeline) in timelines)
        {
            if (timeline.CycleTime() is not { } cycleTime)
            {
                continue;
            }

            var cycleTimeDays = (decimal)(cycleTime.CompletedAt - cycleTime.StartedAt).TotalDays;
            items.Add(new CompletedItemDto(workItemId, cycleTime.StartedAt, cycleTime.CompletedAt, cycleTimeDays));
        }

        return items.OrderBy(item => item.CompletedAt).ToArray();
    }

    private static decimal Median(decimal[] sortedValues)
    {
        var mid = sortedValues.Length / 2;
        return sortedValues.Length % 2 == 0
            ? (sortedValues[mid - 1] + sortedValues[mid]) / 2m
            : sortedValues[mid];
    }
}

public sealed class ControlChartHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    ISprintRepository sprints,
    ISprintScopeFactRepository sprintScopeFacts,
    IWorkItemHistoryRepository history,
    IWorkItemStatusRepository workItemStatuses) : IRequestHandler<ControlChartQuery, ControlChartDto>
{
    public async Task<ControlChartDto> Handle(ControlChartQuery request, CancellationToken cancellationToken)
    {
        var (sprint, _, timelines, _) = await AgileReportData.LoadAsync(
            request.SprintId, tenant, projects, sprints, sprintScopeFacts, history, workItemStatuses, cancellationToken);

        var items = CycleTimeReportHandler.BuildCompletedItems(timelines);
        var cycleTimeDays = items.Select(item => item.CycleTimeDays).OrderBy(days => days).ToArray();

        return new ControlChartDto(
            sprint.Id,
            items,
            cycleTimeDays.Length == 0 ? null : cycleTimeDays.Average(),
            cycleTimeDays.Length == 0 ? null : Percentile85(cycleTimeDays));
    }

    // The 85th percentile is the conventional control-chart threshold line: the cycle time under
    // which 85% of completed items fell, used to spot outliers rather than the noisier max/average.
    private static decimal Percentile85(decimal[] sortedValues)
    {
        if (sortedValues.Length == 1)
        {
            return sortedValues[0];
        }

        var rank = 0.85m * (sortedValues.Length - 1);
        var lowerIndex = (int)Math.Floor(rank);
        var upperIndex = (int)Math.Ceiling(rank);
        var fraction = rank - lowerIndex;
        return sortedValues[lowerIndex] + (sortedValues[upperIndex] - sortedValues[lowerIndex]) * fraction;
    }
}
