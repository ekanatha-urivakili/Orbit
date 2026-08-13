using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Choices;
using Orbit.Domain.Common;

namespace Orbit.Application.Boards;

public sealed record SprintDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Goal,
    SprintState State,
    DateOnly? StartDate,
    DateOnly? EndDate,
    long Version,
    IReadOnlyList<Guid> WorkItemIds)
{
    public static SprintDto From(Sprint sprint, IReadOnlyList<Guid> workItemIds) =>
        new(sprint.Id, sprint.ProjectId, sprint.Name, sprint.Goal, sprint.State, sprint.StartDate, sprint.EndDate, sprint.Version, workItemIds);
}

public sealed record CreateSprintCommand(Guid ProjectId, string Name) : ICommand<SprintDto>;

public sealed class CreateSprintValidator : AbstractValidator<CreateSprintCommand>
{
    public CreateSprintValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().Length(2, 120);
    }
}

public sealed class CreateSprintHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    ISprintRepository sprints,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<CreateSprintCommand, SprintDto>
{
    public async Task<SprintDto> Handle(CreateSprintCommand request, CancellationToken cancellationToken)
    {
        _ = await projects.GetAsync(tenant.TenantId, request.ProjectId, ProjectPermission.TransitionWorkItem, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");

        var sprint = Sprint.Create(tenant.TenantId, request.ProjectId, request.Name, timeProvider.GetUtcNow());
        await sprints.AddAsync(sprint, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SprintDto.From(sprint, []);
    }
}

public sealed record ListSprintsQuery(Guid ProjectId) : IQuery<IReadOnlyList<SprintDto>>;

public sealed class ListSprintsValidator : AbstractValidator<ListSprintsQuery>
{
    public ListSprintsValidator() => RuleFor(query => query.ProjectId).NotEmpty();
}

public sealed class ListSprintsHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    ISprintRepository sprints,
    ISprintMembershipRepository memberships) : IRequestHandler<ListSprintsQuery, IReadOnlyList<SprintDto>>
{
    public async Task<IReadOnlyList<SprintDto>> Handle(ListSprintsQuery request, CancellationToken cancellationToken)
    {
        _ = await projects.GetAsync(tenant.TenantId, request.ProjectId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");

        var projectSprints = await sprints.ListByProjectAsync(tenant.TenantId, request.ProjectId, cancellationToken);
        var sprintIds = projectSprints.Select(sprint => sprint.Id).ToArray();
        var membershipsBySprint = (await memberships.ListCurrentBySprintsAsync(
                tenant.TenantId, sprintIds, cancellationToken))
            .GroupBy(membership => membership.SprintId)
            .ToDictionary(group => group.Key, group => group.Select(membership => membership.WorkItemId).ToArray());
        var result = new List<SprintDto>(projectSprints.Count);
        foreach (var sprint in projectSprints)
        {
            result.Add(SprintDto.From(
                sprint,
                membershipsBySprint.GetValueOrDefault(sprint.Id) ?? []));
        }

        return result;
    }
}

public sealed record StartSprintCommand(
    Guid SprintId,
    string? Goal,
    DateOnly? StartDate,
    DateOnly? EndDate,
    long ExpectedVersion) : ICommand<SprintDto>;

public sealed class StartSprintValidator : AbstractValidator<StartSprintCommand>
{
    public StartSprintValidator()
    {
        RuleFor(command => command.SprintId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
        RuleFor(command => command)
            .Must(command => command.StartDate is null || command.EndDate is null || command.EndDate >= command.StartDate)
            .WithMessage("A sprint's end date cannot be before its start date.");
    }
}

public sealed class StartSprintHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    ISprintRepository sprints,
    ISprintMembershipRepository memberships,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<StartSprintCommand, SprintDto>
{
    public async Task<SprintDto> Handle(StartSprintCommand request, CancellationToken cancellationToken)
    {
        var sprint = await sprints.GetAsync(tenant.TenantId, request.SprintId, cancellationToken)
            ?? throw new NotFoundException("Sprint was not found.");
        _ = await projects.GetAsync(tenant.TenantId, sprint.ProjectId, ProjectPermission.TransitionWorkItem, cancellationToken)
            ?? throw new NotFoundException("Sprint was not found.");

        if (sprint.Version != request.ExpectedVersion)
        {
            throw new ConcurrencyException("The sprint changed after it was loaded.");
        }

        var active = await sprints.GetActiveAsync(tenant.TenantId, sprint.ProjectId, cancellationToken);
        if (active is not null)
        {
            throw new DomainException("This project already has an active sprint.");
        }

        sprint.Start(request.Goal, request.StartDate, request.EndDate, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var members = await memberships.ListCurrentBySprintAsync(tenant.TenantId, sprint.Id, cancellationToken);
        return SprintDto.From(sprint, [.. members.Select(member => member.WorkItemId)]);
    }
}

public sealed record CompleteSprintCommand(
    Guid SprintId,
    long ExpectedVersion,
    Guid? RolloverTargetSprintId) : ICommand<SprintDto>;

public sealed class CompleteSprintValidator : AbstractValidator<CompleteSprintCommand>
{
    public CompleteSprintValidator()
    {
        RuleFor(command => command.SprintId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

/// <summary>
/// Closes a sprint atomically through the Closing state. The operation record makes completed
/// requests idempotent; an interrupted request rolls back as a unit and can be retried safely.
/// </summary>
public sealed class CompleteSprintHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    ISprintRepository sprints,
    ISprintMembershipRepository memberships,
    ISprintCompletionOperationRepository completionOperations,
    ISprintScopeFactRepository facts,
    IWorkItemRepository workItems,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<CompleteSprintCommand, SprintDto>
{
    public async Task<SprintDto> Handle(CompleteSprintCommand request, CancellationToken cancellationToken)
    {
        var sprint = await sprints.GetAsync(tenant.TenantId, request.SprintId, cancellationToken)
            ?? throw new NotFoundException("Sprint was not found.");
        _ = await projects.GetAsync(tenant.TenantId, sprint.ProjectId, ProjectPermission.TransitionWorkItem, cancellationToken)
            ?? throw new NotFoundException("Sprint was not found.");

        var now = timeProvider.GetUtcNow();
        var operation = await completionOperations.GetAsync(tenant.TenantId, sprint.Id, cancellationToken);

        if (operation is { State: SprintCompletionOperationState.Completed })
        {
            var current = await memberships.ListCurrentBySprintAsync(tenant.TenantId, sprint.Id, cancellationToken);
            return SprintDto.From(sprint, [.. current.Select(member => member.WorkItemId)]);
        }

        // The concurrency check only guards *starting* a close against a stale snapshot; once an
        // operation exists the retry is resuming that specific close, not issuing a fresh command,
        // so it isn't tied to the version the client originally loaded.
        if (operation is null && sprint.Version != request.ExpectedVersion)
        {
            throw new ConcurrencyException("The sprint changed after it was loaded.");
        }

        var members = await memberships.ListCurrentBySprintAsync(tenant.TenantId, sprint.Id, cancellationToken);

        if (operation is null)
        {
            if (request.RolloverTargetSprintId is { } targetId)
            {
                if (targetId == sprint.Id)
                {
                    throw new DomainException("A sprint cannot roll over into itself.");
                }

                var rolloverTarget = await sprints.GetAsync(tenant.TenantId, targetId, cancellationToken)
                    ?? throw new NotFoundException("Rollover target sprint was not found.");
                if (rolloverTarget.ProjectId != sprint.ProjectId || rolloverTarget.State != SprintState.Future)
                {
                    throw new DomainException("The rollover target must be a future sprint in the same project.");
                }
            }

            sprint.StartClosing(now);
            operation = SprintCompletionOperation.Create(
                tenant.TenantId, sprint.Id, request.RolloverTargetSprintId, members.Count, now);
            await completionOperations.AddAsync(operation, cancellationToken);
        }

        var remaining = new List<Guid>();
        var workItemsById = (await workItems.ListByIdsAsync(
                tenant.TenantId,
                members.Select(membership => membership.WorkItemId).ToArray(),
                ProjectPermission.View,
                cancellationToken))
            .ToDictionary(workItem => workItem.Id);
        foreach (var membership in members)
        {
            if (workItemsById.TryGetValue(membership.WorkItemId, out var workItem)
                && workItem.Status != WorkItemStatus.Done)
            {
                membership.Remove(now);
                await facts.AddAsync(
                    SprintScopeFact.Create(
                        tenant.TenantId, sprint.Id, membership.WorkItemId, AgileFactType.SprintRemoved, now, now),
                    cancellationToken);
                if (operation.RolloverTargetSprintId is { } targetSprintId)
                {
                    await memberships.AddAsync(
                        SprintMembership.Create(tenant.TenantId, targetSprintId, membership.WorkItemId, now),
                        cancellationToken);
                    await facts.AddAsync(
                        SprintScopeFact.Create(
                            tenant.TenantId, targetSprintId, membership.WorkItemId, AgileFactType.SprintAdded, now, now),
                        cancellationToken);
                }
            }
            else
            {
                remaining.Add(membership.WorkItemId);
            }

        }

        sprint.FinishClosing(now);
        operation.RecordProgress(operation.TotalCount, now);
        operation.MarkCompleted(now);
        await facts.AddAsync(
            SprintScopeFact.Create(tenant.TenantId, sprint.Id, null, AgileFactType.SprintCompleted, now, now),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SprintDto.From(sprint, remaining);
    }
}

public sealed record ReopenSprintCommand(Guid SprintId, long ExpectedVersion) : ICommand<SprintDto>;

public sealed class ReopenSprintValidator : AbstractValidator<ReopenSprintCommand>
{
    public ReopenSprintValidator()
    {
        RuleFor(command => command.SprintId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class ReopenSprintHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    ISprintRepository sprints,
    ISprintMembershipRepository memberships,
    ISprintScopeFactRepository facts,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<ReopenSprintCommand, SprintDto>
{
    public async Task<SprintDto> Handle(ReopenSprintCommand request, CancellationToken cancellationToken)
    {
        var sprint = await sprints.GetAsync(tenant.TenantId, request.SprintId, cancellationToken)
            ?? throw new NotFoundException("Sprint was not found.");
        _ = await projects.GetAsync(tenant.TenantId, sprint.ProjectId, ProjectPermission.TransitionWorkItem, cancellationToken)
            ?? throw new NotFoundException("Sprint was not found.");

        if (sprint.Version != request.ExpectedVersion)
        {
            throw new ConcurrencyException("The sprint changed after it was loaded.");
        }

        var now = timeProvider.GetUtcNow();
        sprint.Reopen(now);
        await facts.AddAsync(
            SprintScopeFact.Create(tenant.TenantId, sprint.Id, null, AgileFactType.SprintReopened, now, now),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var members = await memberships.ListCurrentBySprintAsync(tenant.TenantId, sprint.Id, cancellationToken);
        return SprintDto.From(sprint, [.. members.Select(member => member.WorkItemId)]);
    }
}

public sealed record AssignWorkItemToSprintCommand(Guid WorkItemId, Guid SprintId) : ICommand<SprintDto>;

public sealed class AssignWorkItemToSprintValidator : AbstractValidator<AssignWorkItemToSprintCommand>
{
    public AssignWorkItemToSprintValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.SprintId).NotEmpty();
    }
}

public sealed class AssignWorkItemToSprintHandler(
    ITenantContext tenant,
    IWorkItemRepository workItems,
    ISprintRepository sprints,
    ISprintMembershipRepository memberships,
    ISprintScopeFactRepository facts,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<AssignWorkItemToSprintCommand, SprintDto>
{
    public async Task<SprintDto> Handle(AssignWorkItemToSprintCommand request, CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetAsync(
            tenant.TenantId, request.WorkItemId, ProjectPermission.TransitionWorkItem, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");
        var sprint = await sprints.GetAsync(tenant.TenantId, request.SprintId, cancellationToken)
            ?? throw new NotFoundException("Sprint was not found.");

        if (sprint.ProjectId != workItem.ProjectId)
        {
            throw new DomainException("A work item can only be added to a sprint in the same project.");
        }

        if (sprint.State is SprintState.Closed or SprintState.Closing)
        {
            throw new DomainException("Work items cannot be added to a closed sprint.");
        }

        var currentMembers = (await memberships.ListCurrentBySprintAsync(tenant.TenantId, sprint.Id, cancellationToken))
            .Select(member => member.WorkItemId)
            .ToList();

        if (currentMembers.Contains(workItem.Id))
        {
            return SprintDto.From(sprint, currentMembers);
        }

        var now = timeProvider.GetUtcNow();
        var existing = await memberships.GetCurrentByWorkItemAsync(tenant.TenantId, workItem.Id, cancellationToken);
        if (existing is not null)
        {
            existing.Remove(now);
            await facts.AddAsync(
                SprintScopeFact.Create(
                    tenant.TenantId, existing.SprintId, workItem.Id, AgileFactType.SprintRemoved, now, now),
                cancellationToken);
        }

        await memberships.AddAsync(SprintMembership.Create(tenant.TenantId, sprint.Id, workItem.Id, now), cancellationToken);
        await facts.AddAsync(
            SprintScopeFact.Create(tenant.TenantId, sprint.Id, workItem.Id, AgileFactType.SprintAdded, now, now),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        currentMembers.Add(workItem.Id);
        return SprintDto.From(sprint, currentMembers);
    }
}

public sealed record RemoveWorkItemFromSprintCommand(Guid WorkItemId) : ICommand<SprintDto>;

public sealed class RemoveWorkItemFromSprintValidator : AbstractValidator<RemoveWorkItemFromSprintCommand>
{
    public RemoveWorkItemFromSprintValidator() => RuleFor(command => command.WorkItemId).NotEmpty();
}

public sealed class RemoveWorkItemFromSprintHandler(
    ITenantContext tenant,
    IWorkItemRepository workItems,
    ISprintRepository sprints,
    ISprintMembershipRepository memberships,
    ISprintScopeFactRepository facts,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<RemoveWorkItemFromSprintCommand, SprintDto>
{
    public async Task<SprintDto> Handle(RemoveWorkItemFromSprintCommand request, CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetAsync(
            tenant.TenantId, request.WorkItemId, ProjectPermission.TransitionWorkItem, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");
        var membership = await memberships.GetCurrentByWorkItemAsync(tenant.TenantId, workItem.Id, cancellationToken)
            ?? throw new NotFoundException("Work item is not assigned to a sprint.");
        var sprint = await sprints.GetAsync(tenant.TenantId, membership.SprintId, cancellationToken)
            ?? throw new NotFoundException("Sprint was not found.");

        var now = timeProvider.GetUtcNow();
        membership.Remove(now);
        await facts.AddAsync(
            SprintScopeFact.Create(tenant.TenantId, sprint.Id, workItem.Id, AgileFactType.SprintRemoved, now, now),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var remaining = await memberships.ListCurrentBySprintAsync(tenant.TenantId, sprint.Id, cancellationToken);
        return SprintDto.From(sprint, [.. remaining.Select(member => member.WorkItemId)]);
    }
}
