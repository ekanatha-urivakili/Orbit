using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;

namespace Orbit.Application.WorkItems;

public sealed record ChangeWorkItemStatusCommand(Guid WorkItemId, WorkItemStatus Status, long ExpectedVersion)
    : ICommand<WorkItemDto>;

public sealed class ChangeWorkItemStatusValidator : AbstractValidator<ChangeWorkItemStatusCommand>
{
    public ChangeWorkItemStatusValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.Status).IsInEnum();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class ChangeWorkItemStatusHandler(
    ITenantContext tenantContext,
    IWorkItemRepository workItems,
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

        workItem.ChangeStatus(request.Status, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkItemDto.From(workItem);
    }
}
