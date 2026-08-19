using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;

namespace Orbit.Application.WorkItems;

public sealed record ChangeWorkItemTypeCommand(Guid WorkItemId, WorkItemType NewType, long ExpectedVersion)
    : ICommand<WorkItemDto>;

public sealed class ChangeWorkItemTypeValidator : AbstractValidator<ChangeWorkItemTypeCommand>
{
    public ChangeWorkItemTypeValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.NewType).IsInEnum();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class ChangeWorkItemTypeHandler(
    ITenantContext tenantContext,
    IWorkItemRepository workItems,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<ChangeWorkItemTypeCommand, WorkItemDto>
{
    public async Task<WorkItemDto> Handle(ChangeWorkItemTypeCommand request, CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetAsync(
            tenantContext.TenantId, request.WorkItemId, ProjectPermission.TransitionWorkItem, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        if (workItem.Version != request.ExpectedVersion)
        {
            throw new ConcurrencyException("The work item changed after it was loaded.");
        }

        workItem.ChangeType(request.NewType, timeProvider.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkItemDto.From(workItem);
    }
}
