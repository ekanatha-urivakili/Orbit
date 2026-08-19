using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;

namespace Orbit.Application.WorkItems;

public sealed record DeleteWorkItemCommand(Guid WorkItemId, long ExpectedVersion) : ICommand<Unit>;

public sealed class DeleteWorkItemValidator : AbstractValidator<DeleteWorkItemCommand>
{
    public DeleteWorkItemValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class DeleteWorkItemHandler(
    ITenantContext tenantContext,
    IWorkItemRepository workItems,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteWorkItemCommand, Unit>
{
    public async Task<Unit> Handle(DeleteWorkItemCommand request, CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetAsync(
            tenantContext.TenantId, request.WorkItemId, ProjectPermission.Administer, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        if (workItem.Version != request.ExpectedVersion)
        {
            throw new ConcurrencyException("The work item changed after it was loaded.");
        }

        if (await workItems.HasChildrenAsync(tenantContext.TenantId, workItem.Id, cancellationToken))
        {
            throw new ValidationException(
                "This work item has subtasks or child items. Move or delete them first.");
        }

        await workItems.RemoveAsync(workItem, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
