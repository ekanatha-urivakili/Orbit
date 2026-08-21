using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Common;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

public sealed record ReorderWorkItemCommand(
    Guid WorkItemId,
    Guid? BeforeWorkItemId,
    Guid? AfterWorkItemId,
    long ExpectedVersion) : ICommand<WorkItemDto>;

public sealed class ReorderWorkItemValidator : AbstractValidator<ReorderWorkItemCommand>
{
    public ReorderWorkItemValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
        RuleFor(command => command)
            .Must(command => command.BeforeWorkItemId is not null || command.AfterWorkItemId is not null)
            .WithMessage("Reordering requires a preceding or following work item.");
    }
}

public sealed class ReorderWorkItemHandler(
    ITenantContext tenantContext,
    IWorkItemRepository workItems,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<ReorderWorkItemCommand, WorkItemDto>
{
    private const decimal RankGap = 1024m;

    public async Task<WorkItemDto> Handle(ReorderWorkItemCommand request, CancellationToken cancellationToken)
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

        var before = await GetNeighborAsync(request.BeforeWorkItemId, workItem.ProjectId, cancellationToken);
        var after = await GetNeighborAsync(request.AfterWorkItemId, workItem.ProjectId, cancellationToken);

        var rank = (before, after) switch
        {
            (not null, not null) => (before.Rank + after.Rank) / 2m,
            (not null, null) => before.Rank + RankGap,
            (null, not null) => after.Rank - RankGap,
            (null, null) => throw new DomainException("Reordering requires a preceding or following work item.")
        };

        workItem.Reorder(rank, timeProvider.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkItemDto.From(workItem);
    }

    private async Task<WorkItem?> GetNeighborAsync(
        Guid? neighborId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (neighborId is null)
        {
            return null;
        }

        var neighbor = await workItems.GetAsync(
            tenantContext.TenantId,
            neighborId.Value,
            ProjectPermission.TransitionWorkItem,
            cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        if (neighbor.ProjectId != projectId)
        {
            throw new DomainException("A work item can only be reordered against items in the same project.");
        }

        return neighbor;
    }
}
