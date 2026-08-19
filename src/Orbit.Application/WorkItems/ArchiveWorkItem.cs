using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;

namespace Orbit.Application.WorkItems;

public sealed record ArchiveWorkItemCommand(Guid WorkItemId, long ExpectedVersion) : ICommand<WorkItemDto>;

public sealed class ArchiveWorkItemValidator : AbstractValidator<ArchiveWorkItemCommand>
{
    public ArchiveWorkItemValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class ArchiveWorkItemHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IWorkItemRepository workItems,
    IWorkItemHistoryRepository history,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<ArchiveWorkItemCommand, WorkItemDto>
{
    public async Task<WorkItemDto> Handle(ArchiveWorkItemCommand request, CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetAsync(
            tenantContext.TenantId, request.WorkItemId, ProjectPermission.Administer, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        if (workItem.Version != request.ExpectedVersion)
        {
            throw new ConcurrencyException("The work item changed after it was loaded.");
        }

        var now = timeProvider.GetUtcNow();
        workItem.Archive(now);

        await WorkItemHistoryRecorder.RecordAsync(
            history, tenantContext.TenantId, workItem.Id, principal.MembershipId, now, cancellationToken,
            ("Archived", "No", "Yes"));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkItemDto.From(workItem);
    }
}

public sealed record UnarchiveWorkItemCommand(Guid WorkItemId, long ExpectedVersion) : ICommand<WorkItemDto>;

public sealed class UnarchiveWorkItemValidator : AbstractValidator<UnarchiveWorkItemCommand>
{
    public UnarchiveWorkItemValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class UnarchiveWorkItemHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IWorkItemRepository workItems,
    IWorkItemHistoryRepository history,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<UnarchiveWorkItemCommand, WorkItemDto>
{
    public async Task<WorkItemDto> Handle(UnarchiveWorkItemCommand request, CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetAsync(
            tenantContext.TenantId, request.WorkItemId, ProjectPermission.Administer, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        if (workItem.Version != request.ExpectedVersion)
        {
            throw new ConcurrencyException("The work item changed after it was loaded.");
        }

        var now = timeProvider.GetUtcNow();
        workItem.Unarchive(now);

        await WorkItemHistoryRecorder.RecordAsync(
            history, tenantContext.TenantId, workItem.Id, principal.MembershipId, now, cancellationToken,
            ("Archived", "Yes", "No"));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkItemDto.From(workItem);
    }
}
