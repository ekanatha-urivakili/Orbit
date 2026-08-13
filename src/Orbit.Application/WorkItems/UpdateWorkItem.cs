using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;

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
    decimal? StoryPoints,
    WorkItemLinkType? LinkType,
    Guid? LinkedWorkItemId,
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
        RuleFor(command => command)
            .Must(command => command.LinkType.HasValue == command.LinkedWorkItemId.HasValue)
            .WithMessage("A linked work item and relationship type must be supplied together.");
    }
}

public sealed class UpdateWorkItemHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IWorkItemRepository workItems,
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

        WorkItemRelations.ValidateOwners(
            request.AssigneeUserId, request.DeveloperUserId, request.ProductOwnerUserId, principal.UserId);
        var parent = await WorkItemRelations.GetRelatedItemAsync(
            workItems, tenantContext.TenantId, request.ParentId, workItem.ProjectId, "Parent", cancellationToken);
        WorkItemRelations.ValidateParentType(workItem.Type, parent);
        await WorkItemRelations.GetRelatedItemAsync(
            workItems, tenantContext.TenantId, request.LinkedWorkItemId, workItem.ProjectId, "Linked work item", cancellationToken);

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
            request.StoryPoints,
            request.LinkType,
            request.LinkedWorkItemId,
            request.Labels,
            request.Countries,
            request.AttachmentNames,
            timeProvider.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkItemDto.From(workItem);
    }
}
