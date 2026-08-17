using FluentValidation;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

internal static class WorkItemRelations
{
    public static void ValidateOwners(
        Guid? assigneeUserId,
        Guid? developerUserId,
        Guid? productOwnerUserId,
        Guid? currentUserId)
    {
        var requestedOwners = new[]
        {
            assigneeUserId,
            developerUserId,
            productOwnerUserId
        }.Where(userId => userId.HasValue).Select(userId => userId!.Value);
        if (requestedOwners.Any(userId => userId != currentUserId))
        {
            throw new ValidationException("This increment supports assigning ownership only to the current user.");
        }
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
