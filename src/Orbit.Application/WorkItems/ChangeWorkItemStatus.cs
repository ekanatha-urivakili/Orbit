using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Choices;
using Orbit.Domain.Configuration;
using Orbit.Domain.Messaging;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

public sealed record ChangeWorkItemStatusCommand(Guid WorkItemId, Guid StatusId, long ExpectedVersion)
    : ICommand<WorkItemDto>;

public sealed class ChangeWorkItemStatusValidator : AbstractValidator<ChangeWorkItemStatusCommand>
{
    public ChangeWorkItemStatusValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.StatusId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class ChangeWorkItemStatusHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IWorkItemRepository workItems,
    IWorkItemStatusRepository workItemStatuses,
    ISprintMembershipRepository sprintMemberships,
    ISprintScopeFactRepository sprintScopeFacts,
    ISettingsRepository settings,
    IOutboxRepository outbox,
    IWorkItemHistoryRepository history,
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

        var newStatus = await workItemStatuses.GetAsync(
            tenantContext.TenantId, workItem.ProjectId, request.StatusId, cancellationToken)
            ?? throw new ValidationException("The selected status does not belong to this project's workflow.");
        var previousStatus = await workItemStatuses.GetAsync(
            tenantContext.TenantId, workItem.ProjectId, workItem.StatusId, cancellationToken)
            ?? throw new NotFoundException("The work item's current status no longer exists.");

        var now = timeProvider.GetUtcNow();
        workItem.ChangeStatus(newStatus.Id, now);

        // A burndown only moves when an item crosses the Done boundary while it's sprint-scoped;
        // other status moves (e.g. Backlog -> InProgress) don't change remaining points.
        var enteredDone = previousStatus.Category != StatusCategory.Done && newStatus.Category == StatusCategory.Done;
        var leftDone = previousStatus.Category == StatusCategory.Done && newStatus.Category != StatusCategory.Done;
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

        if (previousStatus.Id != newStatus.Id)
        {
            await NotifyOwnersAsync(workItem, previousStatus, newStatus, now, cancellationToken);
        }

        await WorkItemHistoryRecorder.RecordAsync(
            history, tenantContext.TenantId, workItem.Id, principal.MembershipId, now, cancellationToken,
            ("Status", previousStatus.Key, newStatus.Key));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkItemDto.From(workItem);
    }

    /// <summary>
    /// Fires the §10.5 "status transition" notification: the work item's owner fields
    /// (Assignee/Developer/ProductOwner) are the recipient set, gated by each recipient's
    /// <see cref="Domain.Settings.NotificationPreference"/> the same way as the comment-mention
    /// trigger (a never-touched preference defaults to EmailEnabled = true, SelfNotify = false).
    /// </summary>
    private async Task NotifyOwnersAsync(
        WorkItem workItem,
        WorkItemStatusDefinition previousStatus,
        WorkItemStatusDefinition newStatus,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        Guid?[] owners = [workItem.AssigneeUserId, workItem.DeveloperUserId, workItem.ProductOwnerUserId];
        var recipientIds = owners.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (recipientIds.Length == 0)
        {
            return;
        }

        var accounts = (await settings.GetUserAccountsAsync(recipientIds, cancellationToken))
            .ToDictionary(a => a.Id);

        foreach (var userId in recipientIds)
        {
            if (!accounts.TryGetValue(userId, out var account))
            {
                continue;
            }

            var preference = await settings.GetNotificationPreferenceAsync(userId, cancellationToken);
            var emailEnabled = preference?.EmailEnabled ?? true;
            var selfNotify = preference?.SelfNotify ?? false;
            if (!emailEnabled || (userId == principal.UserId && !selfNotify))
            {
                continue;
            }

            var userPreference = await settings.GetUserPreferenceAsync(userId, cancellationToken);
            var email = OutboxEmailMessage.Create(
                account.NormalizedEmail,
                $"{workItem.Key} moved to {newStatus.Name}",
                $"""
                <p>Hi {System.Net.WebUtility.HtmlEncode(account.DisplayName)},</p>
                <p><strong>{System.Net.WebUtility.HtmlEncode(workItem.Key)}: {System.Net.WebUtility.HtmlEncode(workItem.Summary)}</strong>
                moved from {System.Net.WebUtility.HtmlEncode(previousStatus.Name)} to {System.Net.WebUtility.HtmlEncode(newStatus.Name)}.</p>
                """,
                now);
            email.ScheduleFor(NotificationScheduling.ComputeNotBefore(preference, userPreference?.TimeZone, now));
            await outbox.AddAsync(email, cancellationToken);
        }
    }
}
