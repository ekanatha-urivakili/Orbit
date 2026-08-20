using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

public sealed record ChangeWorkItemAssigneeCommand(Guid WorkItemId, Guid? AssigneeUserId, long ExpectedVersion)
    : ICommand<WorkItemDto>;

public sealed class ChangeWorkItemAssigneeValidator : AbstractValidator<ChangeWorkItemAssigneeCommand>
{
    public ChangeWorkItemAssigneeValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class ChangeWorkItemAssigneeHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IWorkItemRepository workItems,
    ITenantMembershipRepository tenantMemberships,
    ISettingsRepository settings,
    IOutboxRepository outbox,
    IWorkItemHistoryRepository history,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<ChangeWorkItemAssigneeCommand, WorkItemDto>
{
    public async Task<WorkItemDto> Handle(
        ChangeWorkItemAssigneeCommand request,
        CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetAsync(
            tenantContext.TenantId, request.WorkItemId, ProjectPermission.TransitionWorkItem, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        if (workItem.Version != request.ExpectedVersion)
        {
            throw new ConcurrencyException("The work item changed after it was loaded.");
        }

        await WorkItemRelations.ValidateOwnersAsync(
            tenantMemberships, tenantContext.TenantId, request.AssigneeUserId, null, null, cancellationToken);

        var previousAssigneeUserId = workItem.AssigneeUserId;
        var now = timeProvider.GetUtcNow();
        workItem.ChangeAssignee(request.AssigneeUserId, now);

        if (workItem.AssigneeUserId is { } assigneeUserId && assigneeUserId != previousAssigneeUserId)
        {
            await WorkItemRelations.NotifyAssigneeAsync(
                principal, settings, outbox, workItem, assigneeUserId, now, cancellationToken);
        }

        var accounts = (await settings.GetUserAccountsAsync(
            [.. new[] { previousAssigneeUserId, workItem.AssigneeUserId }.Where(id => id.HasValue).Select(id => id!.Value)],
            cancellationToken)).ToDictionary(account => account.Id);
        string? Label(Guid? userId) => userId.HasValue && accounts.TryGetValue(userId.Value, out var account)
            ? account.DisplayName
            : null;

        await WorkItemHistoryRecorder.RecordAsync(
            history, tenantContext.TenantId, workItem.Id, principal.MembershipId, now, cancellationToken,
            ("Assignee", Label(previousAssigneeUserId), Label(workItem.AssigneeUserId)));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkItemDto.From(workItem);
    }
}
