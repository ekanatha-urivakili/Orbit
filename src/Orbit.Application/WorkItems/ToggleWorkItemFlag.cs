using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;

namespace Orbit.Application.WorkItems;

public sealed record ToggleWorkItemFlagCommand(Guid WorkItemId, bool Flagged, long ExpectedVersion)
    : ICommand<WorkItemDto>;

public sealed class ToggleWorkItemFlagValidator : AbstractValidator<ToggleWorkItemFlagCommand>
{
    public ToggleWorkItemFlagValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class ToggleWorkItemFlagHandler(
    ITenantContext tenantContext,
    IWorkItemRepository workItems,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<ToggleWorkItemFlagCommand, WorkItemDto>
{
    public async Task<WorkItemDto> Handle(ToggleWorkItemFlagCommand request, CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetAsync(
            tenantContext.TenantId, request.WorkItemId, ProjectPermission.TransitionWorkItem, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        if (workItem.Version != request.ExpectedVersion)
        {
            throw new ConcurrencyException("The work item changed after it was loaded.");
        }

        workItem.SetFlagged(request.Flagged, timeProvider.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkItemDto.From(workItem);
    }
}
