using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;

namespace Orbit.Application.WorkItems;

public sealed record MoveWorkItemCommand(Guid WorkItemId, Guid TargetProjectId, long ExpectedVersion)
    : ICommand<WorkItemDto>;

public sealed class MoveWorkItemValidator : AbstractValidator<MoveWorkItemCommand>
{
    public MoveWorkItemValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.TargetProjectId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class MoveWorkItemHandler(
    ITenantContext tenantContext,
    IProjectRepository projects,
    IWorkItemRepository workItems,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<MoveWorkItemCommand, WorkItemDto>
{
    public async Task<WorkItemDto> Handle(MoveWorkItemCommand request, CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetAsync(
            tenantContext.TenantId, request.WorkItemId, ProjectPermission.Administer, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        if (workItem.Version != request.ExpectedVersion)
        {
            throw new ConcurrencyException("The work item changed after it was loaded.");
        }

        if (workItem.ProjectId == request.TargetProjectId)
        {
            return WorkItemDto.From(workItem);
        }

        var targetProject = await projects.GetAsync(
            tenantContext.TenantId, request.TargetProjectId, ProjectPermission.Administer, cancellationToken)
            ?? throw new NotFoundException("Target project was not found.");

        var now = timeProvider.GetUtcNow();
        var sequence = targetProject.AllocateItemSequence(now);
        workItem.MoveToProject(targetProject.Id, sequence, targetProject.Key, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkItemDto.From(workItem);
    }
}
