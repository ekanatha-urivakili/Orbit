using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

public sealed record CloneWorkItemCommand(Guid WorkItemId) : ICommand<WorkItemDto>;

public sealed class CloneWorkItemValidator : AbstractValidator<CloneWorkItemCommand>
{
    public CloneWorkItemValidator() => RuleFor(command => command.WorkItemId).NotEmpty();
}

public sealed class CloneWorkItemHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IProjectRepository projects,
    IWorkItemRepository workItems,
    IWorkItemStatusRepository workItemStatuses,
    IWorkItemHistoryRepository history,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<CloneWorkItemCommand, WorkItemDto>
{
    public async Task<WorkItemDto> Handle(CloneWorkItemCommand request, CancellationToken cancellationToken)
    {
        var source = await workItems.GetAsync(
            tenantContext.TenantId, request.WorkItemId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        var project = await projects.GetAsync(
            tenantContext.TenantId, source.ProjectId, ProjectPermission.CreateWorkItem, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");

        var now = timeProvider.GetUtcNow();
        var defaultStatus = await workItemStatuses.GetDefaultAsync(tenantContext.TenantId, project.Id, cancellationToken)
            ?? throw new ValidationException("This project has no workflow statuses configured.");
        var sequence = project.AllocateItemSequence(now);
        var clone = WorkItem.Create(
            tenantContext.TenantId,
            project.Id,
            sequence,
            project.Key,
            $"Copy of {source.Summary}",
            source.Description,
            source.Type,
            source.Priority,
            defaultStatus.Id,
            now);
        clone.SetDetails(
            parentId: source.ParentId,
            epicName: source.EpicName,
            acceptanceCriteria: source.AcceptanceCriteria,
            stepsToConduct: source.StepsToConduct,
            assigneeUserId: null,
            developerUserId: null,
            productOwnerUserId: null,
            sprintName: null,
            identifiedOn: source.IdentifiedOn,
            startDate: source.StartDate,
            dueDate: source.DueDate,
            teamId: source.TeamId,
            storyPoints: source.StoryPoints,
            labels: source.Labels,
            countries: source.Countries,
            attachmentNames: null);

        await workItems.AddAsync(clone, cancellationToken);

        await history.AddAsync(
            WorkItemHistoryEntry.Create(
                tenantContext.TenantId, clone.Id, principal.MembershipId, "Ticket", null, $"Cloned from {source.Key}", now),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkItemDto.From(clone);
    }
}
