using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

// ---------------------------------------------------------------------------
// DTO
// ---------------------------------------------------------------------------

public sealed record WorkItemHistoryEntryDto(
    Guid Id,
    string FieldName,
    string? OldValue,
    string? NewValue,
    Guid? ChangedByUserId,
    string ChangedByDisplayName,
    DateTimeOffset ChangedAt)
{
    public static WorkItemHistoryEntryDto From(
        WorkItemHistoryEntry entry,
        Guid? changedByUserId,
        string? changedByDisplayName) =>
        new(
            entry.Id,
            entry.FieldName,
            entry.OldValue,
            entry.NewValue,
            changedByUserId,
            changedByDisplayName ?? "Unknown member",
            entry.ChangedAt);
}

// ---------------------------------------------------------------------------
// Recorder helper — shared by the mutating handlers
// ---------------------------------------------------------------------------

internal static class WorkItemHistoryRecorder
{
    /// <summary>
    /// Appends one history entry per changed field, skipping any pair where the old and new
    /// values are identical so no-op updates don't produce spurious rows.
    /// </summary>
    public static async Task RecordAsync(
        IWorkItemHistoryRepository history,
        Guid tenantId,
        Guid workItemId,
        Guid changedByMembershipId,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        params (string Field, string? Old, string? New)[] changes)
    {
        foreach (var change in changes)
        {
            if (change.Old == change.New)
            {
                continue;
            }

            await history.AddAsync(
                WorkItemHistoryEntry.Create(
                    tenantId, workItemId, changedByMembershipId, change.Field, change.Old, change.New, now),
                cancellationToken);
        }
    }
}

// ---------------------------------------------------------------------------
// List history
// ---------------------------------------------------------------------------

public sealed record ListWorkItemHistoryQuery(Guid WorkItemId) : IQuery<IReadOnlyList<WorkItemHistoryEntryDto>>;

public sealed class ListWorkItemHistoryValidator : AbstractValidator<ListWorkItemHistoryQuery>
{
    public ListWorkItemHistoryValidator() => RuleFor(query => query.WorkItemId).NotEmpty();
}

public sealed class ListWorkItemHistoryHandler(
    ITenantContext tenantContext,
    IWorkItemRepository workItems,
    IWorkItemHistoryRepository history,
    ITenantMembershipRepository memberships,
    ISettingsRepository settings) : IRequestHandler<ListWorkItemHistoryQuery, IReadOnlyList<WorkItemHistoryEntryDto>>
{
    public async Task<IReadOnlyList<WorkItemHistoryEntryDto>> Handle(
        ListWorkItemHistoryQuery request,
        CancellationToken cancellationToken)
    {
        _ = await workItems.GetAsync(
                tenantContext.TenantId,
                request.WorkItemId,
                ProjectPermission.View,
                cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        var entries = await history.ListByWorkItemAsync(
            tenantContext.TenantId,
            request.WorkItemId,
            cancellationToken);

        var referencedMemberIds = entries
            .Select(entry => entry.ChangedByMembershipId)
            .Distinct()
            .ToArray();

        var tenantMembers = (await memberships.ListByIdsAsync(tenantContext.TenantId, referencedMemberIds, cancellationToken))
            .ToDictionary(m => m.Id);

        var userIds = tenantMembers.Values
            .Where(m => m.UserId.HasValue)
            .Select(m => m.UserId!.Value)
            .Distinct()
            .ToArray();

        var accounts = (await settings.GetUserAccountsAsync(userIds, cancellationToken))
            .ToDictionary(a => a.Id);

        return entries.Select(entry =>
        {
            Guid? changedByUserId = null;
            string? displayName = null;
            if (tenantMembers.TryGetValue(entry.ChangedByMembershipId, out var member))
            {
                if (member.UserId.HasValue && accounts.TryGetValue(member.UserId.Value, out var account))
                {
                    changedByUserId = member.UserId;
                    displayName = account.DisplayName;
                }
                else
                {
                    displayName = member.Subject;
                }
            }

            return WorkItemHistoryEntryDto.From(entry, changedByUserId, displayName);
        }).ToArray();
    }
}
