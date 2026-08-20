using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Choices;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

public sealed record UpdateWorkItemCommand(
    Guid WorkItemId,
    string Summary,
    string? Description,
    Priority Priority,
    Guid? ParentId,
    string? EpicName,
    string? AcceptanceCriteria,
    string? StepsToConduct,
    Guid? AssigneeUserId,
    Guid? DeveloperUserId,
    Guid? ProductOwnerUserId,
    string? SprintName,
    string? IdentifiedOn,
    DateOnly? StartDate,
    DateOnly? DueDate,
    Guid? TeamId,
    decimal? StoryPoints,
    string[]? Labels,
    string[]? Countries,
    string[]? AttachmentNames,
    long ExpectedVersion) : ICommand<WorkItemDto>;

public sealed class UpdateWorkItemValidator : AbstractValidator<UpdateWorkItemCommand>
{
    public UpdateWorkItemValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.Summary).NotEmpty().Length(3, 255);
        RuleFor(command => command.Description).MaximumLength(32_000);
        RuleFor(command => command.Priority).IsInEnum();
        RuleFor(command => command.AcceptanceCriteria).MaximumLength(32_000);
        RuleFor(command => command.StepsToConduct).MaximumLength(32_000);
        RuleFor(command => command.SprintName).MaximumLength(255);
        RuleFor(command => command.IdentifiedOn).MaximumLength(255);
        RuleFor(command => command.StoryPoints).InclusiveBetween(0, 10_000).When(command => command.StoryPoints.HasValue);
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class UpdateWorkItemHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IWorkItemRepository workItems,
    ISprintMembershipRepository sprintMemberships,
    ISprintScopeFactRepository sprintScopeFacts,
    ITenantMembershipRepository tenantMemberships,
    ITeamRepository teams,
    ISettingsRepository settings,
    IOutboxRepository outbox,
    IWorkItemHistoryRepository history,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<UpdateWorkItemCommand, WorkItemDto>
{
    public async Task<WorkItemDto> Handle(UpdateWorkItemCommand request, CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetAsync(
            tenantContext.TenantId, request.WorkItemId, ProjectPermission.TransitionWorkItem, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        if (workItem.Version != request.ExpectedVersion)
        {
            throw new ConcurrencyException("The work item changed after it was loaded.");
        }

        await WorkItemRelations.ValidateOwnersAsync(
            tenantMemberships, tenantContext.TenantId, request.AssigneeUserId, request.DeveloperUserId,
            request.ProductOwnerUserId, cancellationToken);
        var parent = await WorkItemRelations.GetRelatedItemAsync(
            workItems, tenantContext.TenantId, request.ParentId, workItem.ProjectId, "Parent", cancellationToken);
        WorkItemRelations.ValidateParentType(workItem.Type, parent);
        if (request.TeamId is { } teamId
            && await teams.GetAsync(tenantContext.TenantId, teamId, cancellationToken) is null)
        {
            throw new ValidationException("The selected team was not found.");
        }

        var previousStoryPoints = workItem.StoryPoints;
        var previousStatus = workItem.Status;
        var previousAssigneeUserId = workItem.AssigneeUserId;
        var previousSummary = workItem.Summary;
        var previousDescription = workItem.Description;
        var previousPriority = workItem.Priority;
        var previousParentId = workItem.ParentId;
        var previousEpicName = workItem.EpicName;
        var previousAcceptanceCriteria = workItem.AcceptanceCriteria;
        var previousStepsToConduct = workItem.StepsToConduct;
        var previousDeveloperUserId = workItem.DeveloperUserId;
        var previousProductOwnerUserId = workItem.ProductOwnerUserId;
        var previousSprintName = workItem.SprintName;
        var previousIdentifiedOn = workItem.IdentifiedOn;
        var previousStartDate = workItem.StartDate;
        var previousDueDate = workItem.DueDate;
        var previousTeamId = workItem.TeamId;
        var previousLabels = workItem.Labels;
        var previousCountries = workItem.Countries;
        var now = timeProvider.GetUtcNow();
        workItem.Update(
            request.Summary,
            request.Description,
            request.Priority,
            request.ParentId,
            request.EpicName,
            request.AcceptanceCriteria,
            request.StepsToConduct,
            request.AssigneeUserId,
            request.DeveloperUserId,
            request.ProductOwnerUserId,
            request.SprintName,
            request.IdentifiedOn,
            request.StartDate,
            request.DueDate,
            request.TeamId,
            request.StoryPoints,
            request.Labels,
            request.Countries,
            request.AttachmentNames,
            now);

        // Only an estimate change on a not-yet-Done item moves the burndown line; re-pointing a
        // completed item doesn't retroactively change points already burned down.
        if (workItem.StoryPoints != previousStoryPoints && previousStatus != WorkItemStatus.Done)
        {
            var membership = await sprintMemberships.GetCurrentByWorkItemAsync(
                tenantContext.TenantId, workItem.Id, cancellationToken);
            if (membership is not null)
            {
                var delta = (workItem.StoryPoints ?? 0) - (previousStoryPoints ?? 0);
                await sprintScopeFacts.AddAsync(
                    SprintScopeFact.Create(
                        tenantContext.TenantId, membership.SprintId, workItem.Id, AgileFactType.EstimateChanged,
                        delta, now, now),
                    cancellationToken);
            }
        }

        if (workItem.AssigneeUserId is { } assigneeUserId && assigneeUserId != previousAssigneeUserId)
        {
            await WorkItemRelations.NotifyAssigneeAsync(
                principal, settings, outbox, workItem, assigneeUserId, now, cancellationToken);
        }

        await RecordHistoryAsync(
            workItem,
            previousSummary, previousDescription, previousPriority, previousParentId, parent,
            previousEpicName, previousAcceptanceCriteria, previousStepsToConduct,
            previousAssigneeUserId, previousDeveloperUserId, previousProductOwnerUserId,
            previousSprintName, previousIdentifiedOn, previousStartDate, previousDueDate, previousTeamId,
            previousStoryPoints, previousLabels, previousCountries,
            now, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkItemDto.From(workItem);
    }

    private async Task RecordHistoryAsync(
        WorkItem workItem,
        string previousSummary,
        string? previousDescription,
        Priority previousPriority,
        Guid? previousParentId,
        WorkItem? newParent,
        string? previousEpicName,
        string? previousAcceptanceCriteria,
        string? previousStepsToConduct,
        Guid? previousAssigneeUserId,
        Guid? previousDeveloperUserId,
        Guid? previousProductOwnerUserId,
        string? previousSprintName,
        string? previousIdentifiedOn,
        DateOnly? previousStartDate,
        DateOnly? previousDueDate,
        Guid? previousTeamId,
        decimal? previousStoryPoints,
        string[] previousLabels,
        string[] previousCountries,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        string? previousParentLabel = null;
        string? newParentLabel = null;
        if (previousParentId != workItem.ParentId)
        {
            if (previousParentId.HasValue)
            {
                var previousParent = await workItems.GetAsync(
                    tenantContext.TenantId, previousParentId.Value, ProjectPermission.View, cancellationToken);
                previousParentLabel = previousParent is null ? null : $"{previousParent.Key}: {previousParent.Summary}";
            }

            newParentLabel = newParent is null ? null : $"{newParent.Key}: {newParent.Summary}";
        }

        var ownerUserIds = new[]
        {
            previousAssigneeUserId, workItem.AssigneeUserId,
            previousDeveloperUserId, workItem.DeveloperUserId,
            previousProductOwnerUserId, workItem.ProductOwnerUserId,
        }.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();

        var ownerAccounts = ownerUserIds.Length > 0
            ? (await settings.GetUserAccountsAsync(ownerUserIds, cancellationToken)).ToDictionary(a => a.Id)
            : [];

        string? OwnerLabel(Guid? userId) =>
            userId.HasValue && ownerAccounts.TryGetValue(userId.Value, out var account) ? account.DisplayName : null;

        async Task<string?> TeamLabelAsync(Guid? teamId) =>
            teamId is { } id
                ? (await teams.GetAsync(tenantContext.TenantId, id, cancellationToken))?.Name
                : null;

        string? previousTeamLabel = null;
        string? newTeamLabel = null;
        if (previousTeamId != workItem.TeamId)
        {
            previousTeamLabel = await TeamLabelAsync(previousTeamId);
            newTeamLabel = await TeamLabelAsync(workItem.TeamId);
        }

        await WorkItemHistoryRecorder.RecordAsync(
            history, tenantContext.TenantId, workItem.Id, principal.MembershipId, now, cancellationToken,
            ("Summary", previousSummary, workItem.Summary),
            ("Description", previousDescription, workItem.Description),
            ("Priority", previousPriority.ToString(), workItem.Priority.ToString()),
            ("Parent", previousParentLabel, newParentLabel),
            ("Epic", previousEpicName, workItem.EpicName),
            ("Acceptance criteria", previousAcceptanceCriteria, workItem.AcceptanceCriteria),
            ("Steps to conduct", previousStepsToConduct, workItem.StepsToConduct),
            ("Assignee", OwnerLabel(previousAssigneeUserId), OwnerLabel(workItem.AssigneeUserId)),
            ("Developer", OwnerLabel(previousDeveloperUserId), OwnerLabel(workItem.DeveloperUserId)),
            ("Product owner", OwnerLabel(previousProductOwnerUserId), OwnerLabel(workItem.ProductOwnerUserId)),
            ("Sprint", previousSprintName, workItem.SprintName),
            ("Identified on", previousIdentifiedOn, workItem.IdentifiedOn),
            ("Start date", previousStartDate?.ToString("yyyy-MM-dd"), workItem.StartDate?.ToString("yyyy-MM-dd")),
            ("Due date", previousDueDate?.ToString("yyyy-MM-dd"), workItem.DueDate?.ToString("yyyy-MM-dd")),
            ("Team", previousTeamLabel, newTeamLabel),
            ("Story points", previousStoryPoints?.ToString(), workItem.StoryPoints?.ToString()),
            ("Labels", string.Join(", ", previousLabels), string.Join(", ", workItem.Labels)),
            ("Countries", string.Join(", ", previousCountries), string.Join(", ", workItem.Countries)));
    }
}
