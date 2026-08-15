using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Choices;

namespace Orbit.Application.WorkItems;

public sealed record ChangeWorkItemStatusCommand(Guid WorkItemId, WorkItemStatus Status, long ExpectedVersion)
    : ICommand<WorkItemDto>;

public sealed class ChangeWorkItemStatusValidator : AbstractValidator<ChangeWorkItemStatusCommand>
{
    public ChangeWorkItemStatusValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.Status).IsInEnum();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class ChangeWorkItemStatusHandler(
    ITenantContext tenantContext,
    IWorkItemRepository workItems,
    ISprintMembershipRepository sprintMemberships,
    ISprintScopeFactRepository sprintScopeFacts,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<ChangeWorkItemStatusCommand, WorkItemDto>
{
    public async Task<WorkItemDto> Handle(
        ChangeWorkItemStatusCommand request,
        CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetAsync(
            tenantContext.TenantId,
            request.WorkItemId,
            ProjectPermission.TransitionWorkItem,
            cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        if (workItem.Version != request.ExpectedVersion)
        {
            throw new ConcurrencyException("The work item changed after it was loaded.");
        }

        var previousStatus = workItem.Status;
        var now = timeProvider.GetUtcNow();
        workItem.ChangeStatus(request.Status, now);

        // A burndown only moves when an item crosses the Done boundary while it's sprint-scoped;
        // other status moves (e.g. Backlog -> InProgress) don't change remaining points.
        var enteredDone = previousStatus != WorkItemStatus.Done && request.Status == WorkItemStatus.Done;
        var leftDone = previousStatus == WorkItemStatus.Done && request.Status != WorkItemStatus.Done;
        if (enteredDone || leftDone)
        {
            var membership = await sprintMemberships.GetCurrentByWorkItemAsync(
                tenantContext.TenantId, workItem.Id, cancellationToken);
            if (membership is not null)
            {
                var delta = enteredDone ? -(workItem.StoryPoints ?? 0) : (workItem.StoryPoints ?? 0);
                await sprintScopeFacts.AddAsync(
                    SprintScopeFact.Create(
                        tenantContext.TenantId, membership.SprintId, workItem.Id, AgileFactType.StatusChanged,
                        delta, now, now),
                    cancellationToken);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkItemDto.From(workItem);
    }
}
