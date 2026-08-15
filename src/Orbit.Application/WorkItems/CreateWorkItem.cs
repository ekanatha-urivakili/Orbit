using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

public sealed record CreateWorkItemCommand(
    Guid ProjectId,
    string Summary,
    string? Description,
    WorkItemType Type,
    Priority Priority,
    Guid? ParentId = null,
    string? EpicName = null,
    string? AcceptanceCriteria = null,
    string? StepsToConduct = null,
    Guid? AssigneeUserId = null,
    Guid? DeveloperUserId = null,
    Guid? ProductOwnerUserId = null,
    string? SprintName = null,
    string? IdentifiedOn = null,
    decimal? StoryPoints = null,
    WorkItemLinkType? LinkType = null,
    Guid? LinkedWorkItemId = null,
    string[]? Labels = null,
    string[]? Countries = null,
    string[]? AttachmentNames = null) : ICommand<WorkItemDto>;

public sealed class CreateWorkItemValidator : AbstractValidator<CreateWorkItemCommand>
{
    public CreateWorkItemValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Summary).NotEmpty().Length(3, 255);
        RuleFor(command => command.Description).MaximumLength(32_000);
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.Priority).IsInEnum();
        RuleFor(command => command.EpicName)
            .NotEmpty()
            .MaximumLength(255)
            .When(command => command.Type == WorkItemType.Epic);
        RuleFor(command => command.AcceptanceCriteria).MaximumLength(32_000);
        RuleFor(command => command.StepsToConduct).MaximumLength(32_000);
        RuleFor(command => command.SprintName).MaximumLength(255);
        RuleFor(command => command.IdentifiedOn).MaximumLength(255);
        RuleFor(command => command.StoryPoints).InclusiveBetween(0, 10_000).When(command => command.StoryPoints.HasValue);
        RuleFor(command => command)
            .Must(command => command.LinkType.HasValue == command.LinkedWorkItemId.HasValue)
            .WithMessage("A linked work item and relationship type must be supplied together.");
    }
}

public sealed class CreateWorkItemHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IProjectRepository projects,
    IWorkItemTypeRepository workItemTypes,
    IWorkItemRepository workItems,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<CreateWorkItemCommand, WorkItemDto>
{
    public async Task<WorkItemDto> Handle(CreateWorkItemCommand request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(
            tenantContext.TenantId,
            request.ProjectId,
            ProjectPermission.CreateWorkItem,
            cancellationToken)
            ?? throw new NotFoundException("Project was not found.");
        var now = timeProvider.GetUtcNow();
        var itemType = await workItemTypes.GetAsync(tenantContext.TenantId, request.Type, cancellationToken)
            ?? throw new ValidationException("The selected work item type is not configured for this workspace.");
        if (!itemType.Enabled)
        {
            throw new ValidationException("The selected work item type is disabled.");
        }

        WorkItemRelations.ValidateOwners(
            request.AssigneeUserId, request.DeveloperUserId, request.ProductOwnerUserId, principal.UserId);
        var parent = await WorkItemRelations.GetRelatedItemAsync(
            workItems, tenantContext.TenantId, request.ParentId, project.Id, "Parent", cancellationToken);
        WorkItemRelations.ValidateParentType(request.Type, parent);
        await WorkItemRelations.GetRelatedItemAsync(
            workItems, tenantContext.TenantId, request.LinkedWorkItemId, project.Id, "Linked work item", cancellationToken);
        var sequence = project.AllocateItemSequence(now);
        var workItem = WorkItem.Create(
            tenantContext.TenantId,
            project.Id,
            sequence,
            project.Key,
            request.Summary,
            request.Description,
            request.Type,
            request.Priority,
            now);
        workItem.SetDetails(
            request.ParentId,
            request.EpicName,
            request.AcceptanceCriteria,
            request.StepsToConduct,
            request.AssigneeUserId,
            request.DeveloperUserId,
            request.ProductOwnerUserId,
            request.SprintName,
            request.IdentifiedOn,
            request.StoryPoints,
            request.LinkType,
            request.LinkedWorkItemId,
            request.Labels,
            request.Countries,
            request.AttachmentNames);

        await workItems.AddAsync(workItem, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkItemDto.From(workItem);
    }
}
