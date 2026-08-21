using FluentValidation;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.Messaging;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

internal static class WorkItemRelations
{
    /// <summary>
    /// Each requested owner (Assignee/Developer/ProductOwner) must be an active member of the
    /// tenant - opened up from the earlier self-assignment-only rule now that there's a directory
    /// of members to assign to (§10.5's "assigned to a work item" trigger fires off this).
    /// </summary>
    public static async Task ValidateOwnersAsync(
        ITenantMembershipRepository memberships,
        Guid tenantId,
        Guid? assigneeUserId,
        Guid? developerUserId,
        Guid? productOwnerUserId,
        CancellationToken cancellationToken)
    {
        var requestedOwners = new[]
        {
            assigneeUserId,
            developerUserId,
            productOwnerUserId
        }.Where(userId => userId.HasValue).Select(userId => userId!.Value).Distinct().ToArray();
        if (requestedOwners.Length == 0)
        {
            return;
        }

        var activeUserIds = await memberships.ListActiveUserIdsAsync(tenantId, requestedOwners, cancellationToken);
        if (activeUserIds.Count != requestedOwners.Length)
        {
            throw new ValidationException("Owners must be active members of this workspace.");
        }
    }

    /// <summary>
    /// Fires the §10.5 "assigned to a work item" notification for the new assignee, gated by the
    /// same <see cref="Domain.Settings.NotificationPreference"/> check as the other triggers.
    /// </summary>
    public static async Task NotifyAssigneeAsync(
        ICurrentPrincipal principal,
        ISettingsRepository settings,
        IOutboxRepository outbox,
        WorkItem workItem,
        Guid assigneeUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var account = await settings.GetUserAccountAsync(assigneeUserId, cancellationToken);
        if (account is null)
        {
            return;
        }

        var preference = await settings.GetNotificationPreferenceAsync(assigneeUserId, cancellationToken);
        var emailEnabled = preference?.EmailEnabled ?? true;
        var selfNotify = preference?.SelfNotify ?? false;
        if (!emailEnabled || (assigneeUserId == principal.UserId && !selfNotify))
        {
            return;
        }

        var userPreference = await settings.GetUserPreferenceAsync(assigneeUserId, cancellationToken);
        var email = OutboxEmailMessage.Create(
            account.NormalizedEmail,
            $"You were assigned to {workItem.Key}",
            $"""
            <p>Hi {System.Net.WebUtility.HtmlEncode(account.DisplayName)},</p>
            <p>You were assigned to <strong>{System.Net.WebUtility.HtmlEncode(workItem.Key)}: {System.Net.WebUtility.HtmlEncode(workItem.Summary)}</strong>.</p>
            """,
            now);
        email.ScheduleFor(NotificationScheduling.ComputeNotBefore(preference, userPreference?.TimeZone, now));
        await outbox.AddAsync(email, cancellationToken);
    }

    public static async Task<WorkItem?> GetRelatedItemAsync(
        IWorkItemRepository workItems,
        Guid tenantId,
        Guid? workItemId,
        Guid projectId,
        string field,
        CancellationToken cancellationToken)
    {
        if (!workItemId.HasValue)
        {
            return null;
        }

        var item = await workItems.GetAsync(tenantId, workItemId.Value, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException($"{field} was not found.");
        if (item.ProjectId != projectId)
        {
            throw new ValidationException($"{field} must belong to the selected project.");
        }

        return item;
    }

    public static void ValidateParentType(WorkItemType childType, WorkItem? parent)
    {
        if (parent is null)
        {
            return;
        }

        // Any non-Epic, non-Initiative type may nest under any other non-Initiative item, matching
        // the "create a subtask of any type under any ticket" flow on the work item detail page.
        var valid = childType switch
        {
            WorkItemType.Initiative => false,
            WorkItemType.Epic => parent.Type == WorkItemType.Initiative,
            _ => true
        };
        if (!valid)
        {
            throw new ValidationException("The selected parent is outside the work item hierarchy.");
        }
    }
}
