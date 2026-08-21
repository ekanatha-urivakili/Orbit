using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Choices;

namespace Orbit.Application.Boards;

public sealed record BurndownPointDto(DateOnly Date, decimal RemainingPoints);

public sealed record SprintScopeChangeDto(
    Guid? WorkItemId,
    AgileFactType FactType,
    decimal? EstimateDelta,
    DateTimeOffset OccurredAt);

public sealed record SprintReportDto(
    Guid SprintId,
    string SprintName,
    SprintState State,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal CommittedPoints,
    decimal CompletedPoints,
    decimal AddedAfterStartPoints,
    decimal RemovedAfterStartPoints,
    IReadOnlyList<BurndownPointDto> Burndown,
    IReadOnlyList<SprintScopeChangeDto> ScopeChanges);

public sealed record SprintReportQuery(Guid SprintId) : IQuery<SprintReportDto>;

public sealed class SprintReportValidator : AbstractValidator<SprintReportQuery>
{
    public SprintReportValidator() => RuleFor(query => query.SprintId).NotEmpty();
}

/// <summary>
/// Folds the immutable <see cref="SprintScopeFact"/> log for one sprint into a burndown series and
/// scope-change/velocity summary. Reads only append-only facts (never current work-item state), so a
/// closed sprint's report stays reproducible even if its items are later edited or deleted (NFR-14).
/// </summary>
public sealed class SprintReportHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    ISprintRepository sprints,
    ISprintScopeFactRepository facts) : IRequestHandler<SprintReportQuery, SprintReportDto>
{
    public async Task<SprintReportDto> Handle(SprintReportQuery request, CancellationToken cancellationToken)
    {
        var sprint = await sprints.GetAsync(tenant.TenantId, request.SprintId, cancellationToken)
            ?? throw new NotFoundException("Sprint was not found.");
        _ = await projects.GetAsync(tenant.TenantId, sprint.ProjectId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Sprint was not found.");

        var sprintFacts = await facts.ListBySprintAsync(tenant.TenantId, sprint.Id, cancellationToken);
        return Fold(sprint, sprintFacts);
    }

    private static SprintReportDto Fold(Sprint sprint, IReadOnlyList<SprintScopeFact> facts)
    {
        var (committedPoints, completedPoints, addedAfterStartPoints, removedAfterStartPoints) =
            SprintReportPoints.Compute(sprint, facts);

        var burndown = BuildBurndown(sprint, facts);

        var scopeChanges = facts
            .Where(fact => fact.FactType is AgileFactType.SprintAdded or AgileFactType.SprintRemoved)
            .Select(fact => new SprintScopeChangeDto(fact.WorkItemId, fact.FactType, fact.EstimateDelta, fact.OccurredAt))
            .OrderBy(change => change.OccurredAt)
            .ToArray();

        return new SprintReportDto(
            sprint.Id,
            sprint.Name,
            sprint.State,
            sprint.StartDate,
            sprint.EndDate,
            committedPoints,
            completedPoints,
            addedAfterStartPoints,
            removedAfterStartPoints,
            burndown,
            scopeChanges);
    }

    private static IReadOnlyList<BurndownPointDto> BuildBurndown(Sprint sprint, IReadOnlyList<SprintScopeFact> facts)
    {
        if (sprint.StartDate is not { } start)
        {
            // A sprint that never started has no burndown baseline to chart from.
            return [];
        }

        var end = sprint.EndDate ?? DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        if (end < start)
        {
            end = start;
        }

        var ordered = facts
            .Where(fact => fact.EstimateDelta.HasValue)
            .OrderBy(fact => fact.OccurredAt)
            .ToArray();

        var points = new List<BurndownPointDto>();
        var running = 0m;
        var factIndex = 0;
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            var dayEnd = new DateTimeOffset(day.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            while (factIndex < ordered.Length && ordered[factIndex].OccurredAt <= dayEnd)
            {
                running += ordered[factIndex].EstimateDelta!.Value;
                factIndex++;
            }

            points.Add(new BurndownPointDto(day, running));
        }

        return points;
    }
}
