using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

public sealed record WorkItemWorklogDto(
    Guid Id,
    Guid WorkItemId,
    Guid AuthorMembershipId,
    int MinutesSpent,
    DateOnly WorkDate,
    string? Description,
    DateTimeOffset CreatedAt)
{
    public static WorkItemWorklogDto From(WorkItemWorklog worklog) =>
        new(
            worklog.Id,
            worklog.WorkItemId,
            worklog.AuthorMembershipId,
            worklog.MinutesSpent,
            worklog.WorkDate,
            worklog.Description,
            worklog.CreatedAt);
}

public sealed record AddWorklogCommand(
    Guid WorkItemId, int MinutesSpent, DateOnly WorkDate, string? Description) : ICommand<WorkItemWorklogDto>;

public sealed class AddWorklogValidator : AbstractValidator<AddWorklogCommand>
{
    public AddWorklogValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.MinutesSpent).InclusiveBetween(1, 1440);
        RuleFor(command => command.Description).MaximumLength(2_000);
    }
}

public sealed class AddWorklogHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IWorkItemRepository workItems,
    IWorkItemWorklogRepository worklogs,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<AddWorklogCommand, WorkItemWorklogDto>
{
    public async Task<WorkItemWorklogDto> Handle(AddWorklogCommand request, CancellationToken cancellationToken)
    {
        _ = await workItems.GetAsync(
                tenantContext.TenantId, request.WorkItemId, ProjectPermission.TransitionWorkItem, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        var worklog = WorkItemWorklog.Create(
            tenantContext.TenantId,
            request.WorkItemId,
            principal.MembershipId,
            request.MinutesSpent,
            request.WorkDate,
            request.Description,
            timeProvider.GetUtcNow());

        await worklogs.AddAsync(worklog, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkItemWorklogDto.From(worklog);
    }
}

public sealed record ListWorklogsQuery(Guid WorkItemId) : IQuery<IReadOnlyList<WorkItemWorklogDto>>;

public sealed class ListWorklogsValidator : AbstractValidator<ListWorklogsQuery>
{
    public ListWorklogsValidator() => RuleFor(query => query.WorkItemId).NotEmpty();
}

public sealed class ListWorklogsHandler(
    ITenantContext tenantContext,
    IWorkItemRepository workItems,
    IWorkItemWorklogRepository worklogs) : IRequestHandler<ListWorklogsQuery, IReadOnlyList<WorkItemWorklogDto>>
{
    public async Task<IReadOnlyList<WorkItemWorklogDto>> Handle(
        ListWorklogsQuery request, CancellationToken cancellationToken)
    {
        _ = await workItems.GetAsync(
                tenantContext.TenantId, request.WorkItemId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        var current = await worklogs.ListByWorkItemAsync(tenantContext.TenantId, request.WorkItemId, cancellationToken);
        return current.Select(WorkItemWorklogDto.From).ToArray();
    }
}

public sealed record DeleteWorklogCommand(Guid WorkItemId, Guid WorklogId) : ICommand<Unit>;

public sealed class DeleteWorklogValidator : AbstractValidator<DeleteWorklogCommand>
{
    public DeleteWorklogValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.WorklogId).NotEmpty();
    }
}

public sealed class DeleteWorklogHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IWorkItemWorklogRepository worklogs,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteWorklogCommand, Unit>
{
    public async Task<Unit> Handle(DeleteWorklogCommand request, CancellationToken cancellationToken)
    {
        var worklog = await worklogs.GetAsync(tenantContext.TenantId, request.WorklogId, cancellationToken)
            ?? throw new NotFoundException("Work log entry was not found.");

        if (worklog.WorkItemId != request.WorkItemId)
        {
            throw new NotFoundException("Work log entry was not found.");
        }

        if (worklog.AuthorMembershipId != principal.MembershipId)
        {
            throw new NotFoundException("Work log entry was not found.");
        }

        await worklogs.RemoveAsync(worklog, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
