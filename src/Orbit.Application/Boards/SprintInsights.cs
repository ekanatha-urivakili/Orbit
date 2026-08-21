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

// The Board "Sprint insights" panel (ORBIT-WORK-MANAGEMENT-ARCHITECTURE.md §13.5 next-increment):
// a single read model combining sprint progress, "work items for attention" (overdue/stuck/
// blocked/flagged), scope-change (reused from SprintReportHandler's committed/added/removed
// points), and per-epic completion. Reads *current* WorkItem state (unlike the reproducible
// burndown/cycle-time reports in SprintReport.cs/AgileReports.cs, which intentionally fold
// history so a closed sprint stays reproducible) because this panel is explicitly a live,
// "what needs my attention right now" view of an active sprint, not an audit trail.

public sealed record SprintAttentionItemDto(
    Guid WorkItemId,
    string Key,
    string Summary,
    Guid StatusId,
    string StatusName,
    DateOnly? DueDate,
    bool IsFlagged,
    bool IsBlocked,
    bool IsStuck,
    bool IsOverdue);

public sealed record EpicProgressDto(Guid EpicId, string Key, string Name, int TotalCount, int DoneCount, decimal PercentDone);

public sealed record SprintInsightsDto(
    Guid SprintId,
    string SprintName,
    SprintState State,
    int TotalItems,
    int DoneItems,
    int InProgressItems,
    int NotStartedItems,
    decimal PercentDone,
    decimal CommittedPoints,
    decimal CompletedPoints,
    decimal AddedAfterStartPoints,
    decimal RemovedAfterStartPoints,
    IReadOnlyList<SprintAttentionItemDto> ItemsForAttention,
    IReadOnlyList<EpicProgressDto> Epics);

public sealed record SprintInsightsQuery(Guid SprintId) : IQuery<SprintInsightsDto>;

public sealed class SprintInsightsValidator : AbstractValidator<SprintInsightsQuery>
{
    public SprintInsightsValidator() => RuleFor(query => query.SprintId).NotEmpty();
}

public sealed class SprintInsightsHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    ISprintRepository sprints,
    ISprintMembershipRepository memberships,
    ISprintScopeFactRepository sprintScopeFacts,
    IWorkItemRepository workItems,
    IWorkItemStatusRepository workItemStatuses,
    TimeProvider timeProvider) : IRequestHandler<SprintInsightsQuery, SprintInsightsDto>
{
    // A work item with no history/status change in this long is surfaced as "Stuck" while its
    // status is still InProgress-category - a simple staleness heuristic, not a workflow SLA.
    private static readonly TimeSpan StuckThreshold = TimeSpan.FromDays(5);

    public async Task<SprintInsightsDto> Handle(SprintInsightsQuery request, CancellationToken cancellationToken)
    {
        var sprint = await sprints.GetAsync(tenant.TenantId, request.SprintId, cancellationToken)
            ?? throw new NotFoundException("Sprint was not found.");
        _ = await projects.GetAsync(tenant.TenantId, sprint.ProjectId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Sprint was not found.");

        var memberIds = (await memberships.ListCurrentBySprintAsync(tenant.TenantId, sprint.Id, cancellationToken))
            .Select(membership => membership.WorkItemId)
            .ToArray();
        var members = await workItems.ListByIdsAsync(
            tenant.TenantId, memberIds, ProjectPermission.View, cancellationToken);
        var statuses = (await workItemStatuses.ListByProjectAsync(tenant.TenantId, sprint.ProjectId, cancellationToken))
            .ToDictionary(status => status.Id);

        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var doneCount = 0;
        var inProgressCount = 0;
        var attention = new List<SprintAttentionItemDto>();
        foreach (var item in members)
        {
            if (!statuses.TryGetValue(item.StatusId, out var status))
            {
                continue;
            }

            if (status.Category == StatusCategory.Done)
            {
                doneCount++;
            }
            else if (status.Category == StatusCategory.InProgress)
            {
                inProgressCount++;
            }

            var isOverdue = item.DueDate is { } due && due < today && status.Category != StatusCategory.Done;
            var isBlocked = status.Key == "blocked";
            var isStuck = status.Category == StatusCategory.InProgress && now - item.UpdatedAt > StuckThreshold;
            if (isOverdue || isBlocked || isStuck || item.IsFlagged)
            {
                attention.Add(new SprintAttentionItemDto(
                    item.Id, item.Key, item.Summary, status.Id, status.Name, item.DueDate,
                    item.IsFlagged, isBlocked, isStuck, isOverdue));
            }
        }

        var epics = await BuildEpicProgressAsync(members, statuses, cancellationToken);

        var facts = await sprintScopeFacts.ListBySprintAsync(tenant.TenantId, sprint.Id, cancellationToken);
        var (committed, completed, added, removed) = SprintReportPoints.Compute(sprint, facts);

        var total = members.Count;
        return new SprintInsightsDto(
            sprint.Id,
            sprint.Name,
            sprint.State,
            total,
            doneCount,
            inProgressCount,
            total - doneCount - inProgressCount,
            total == 0 ? 0 : Math.Round(100m * doneCount / total, 0),
            committed,
            completed,
            added,
            removed,
            [.. attention.OrderByDescending(item => item.IsOverdue).ThenByDescending(item => item.IsBlocked)],
            epics);
    }

    private async Task<IReadOnlyList<EpicProgressDto>> BuildEpicProgressAsync(
        IReadOnlyList<WorkItem> members,
        IReadOnlyDictionary<Guid, WorkItemStatusDefinition> statuses,
        CancellationToken cancellationToken)
    {
        var epicIds = members.Where(item => item.ParentId.HasValue).Select(item => item.ParentId!.Value).Distinct().ToArray();
        if (epicIds.Length == 0)
        {
            return [];
        }

        var epics = await workItems.ListByIdsAsync(tenant.TenantId, epicIds, ProjectPermission.View, cancellationToken);
        var epicsById = epics.ToDictionary(epic => epic.Id);
        var childrenByEpicId = members
            .Where(item => item.ParentId.HasValue)
            .ToLookup(item => item.ParentId!.Value);

        var result = new List<EpicProgressDto>();
        foreach (var epicId in epicIds)
        {
            if (!epicsById.TryGetValue(epicId, out var epic))
            {
                continue;
            }

            var children = childrenByEpicId[epicId].ToArray();
            var doneChildren = children.Count(item =>
                statuses.TryGetValue(item.StatusId, out var status) && status.Category == StatusCategory.Done);
            result.Add(new EpicProgressDto(
                epic.Id, epic.Key, epic.Summary, children.Length, doneChildren,
                children.Length == 0 ? 0 : Math.Round(100m * doneChildren / children.Length, 0)));
        }

        return result;
    }
}

/// <summary>Shared with <see cref="SprintReportHandler"/>: committed/completed/added/removed points from the scope-fact log.</summary>
internal static class SprintReportPoints
{
    public static (decimal Committed, decimal Completed, decimal Added, decimal Removed) Compute(
        Sprint sprint, IReadOnlyList<SprintScopeFact> facts)
    {
        // Day-granularity threshold: a sprint's StartDate/EndDate are calendar days, so "at start" means
        // any fact recorded on or before the last instant of that UTC day.
        DateTimeOffset? startThreshold = sprint.StartDate is { } startDate
            ? new DateTimeOffset(startDate.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero)
            : null;

        var committed = startThreshold is null
            ? 0m
            : facts
                .Where(fact => fact.FactType is AgileFactType.SprintAdded or AgileFactType.SprintRemoved
                    && fact.OccurredAt <= startThreshold)
                .Sum(fact => fact.EstimateDelta ?? 0);

        var completed = -facts
            .Where(fact => fact.FactType == AgileFactType.StatusChanged)
            .Sum(fact => fact.EstimateDelta ?? 0);

        var added = startThreshold is null
            ? 0m
            : facts
                .Where(fact => fact.FactType == AgileFactType.SprintAdded && fact.OccurredAt > startThreshold)
                .Sum(fact => fact.EstimateDelta ?? 0);

        var removed = startThreshold is null
            ? 0m
            : -facts
                .Where(fact => fact.FactType == AgileFactType.SprintRemoved && fact.OccurredAt > startThreshold)
                .Sum(fact => fact.EstimateDelta ?? 0);

        return (committed, completed, added, removed);
    }
}
