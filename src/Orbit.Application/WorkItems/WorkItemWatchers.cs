using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

public sealed record WatchWorkItemCommand(Guid WorkItemId) : ICommand<Unit>;

public sealed class WatchWorkItemValidator : AbstractValidator<WatchWorkItemCommand>
{
    public WatchWorkItemValidator() => RuleFor(command => command.WorkItemId).NotEmpty();
}

public sealed class WatchWorkItemHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IWorkItemRepository workItems,
    IWorkItemWatcherRepository watchers,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<WatchWorkItemCommand, Unit>
{
    public async Task<Unit> Handle(WatchWorkItemCommand request, CancellationToken cancellationToken)
    {
        var userId = PrincipalGuards.RequireUser(principal);
        _ = await workItems.GetAsync(
                tenantContext.TenantId, request.WorkItemId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        var existing = await watchers.GetAsync(
            tenantContext.TenantId, request.WorkItemId, userId, cancellationToken);
        if (existing is not null)
        {
            return Unit.Value;
        }

        await watchers.AddAsync(
            WorkItemWatcher.Create(tenantContext.TenantId, request.WorkItemId, userId, timeProvider.GetUtcNow()),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record UnwatchWorkItemCommand(Guid WorkItemId) : ICommand<Unit>;

public sealed class UnwatchWorkItemValidator : AbstractValidator<UnwatchWorkItemCommand>
{
    public UnwatchWorkItemValidator() => RuleFor(command => command.WorkItemId).NotEmpty();
}

public sealed class UnwatchWorkItemHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IWorkItemWatcherRepository watchers,
    IUnitOfWork unitOfWork) : IRequestHandler<UnwatchWorkItemCommand, Unit>
{
    public async Task<Unit> Handle(UnwatchWorkItemCommand request, CancellationToken cancellationToken)
    {
        var userId = PrincipalGuards.RequireUser(principal);
        var existing = await watchers.GetAsync(
            tenantContext.TenantId, request.WorkItemId, userId, cancellationToken);
        if (existing is null)
        {
            return Unit.Value;
        }

        await watchers.RemoveAsync(existing, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record WorkItemWatchersDto(bool IsWatching, int Count);

public sealed record GetWorkItemWatchersQuery(Guid WorkItemId) : IQuery<WorkItemWatchersDto>;

public sealed class GetWorkItemWatchersValidator : AbstractValidator<GetWorkItemWatchersQuery>
{
    public GetWorkItemWatchersValidator() => RuleFor(query => query.WorkItemId).NotEmpty();
}

public sealed class GetWorkItemWatchersHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IWorkItemRepository workItems,
    IWorkItemWatcherRepository watchers) : IRequestHandler<GetWorkItemWatchersQuery, WorkItemWatchersDto>
{
    public async Task<WorkItemWatchersDto> Handle(
        GetWorkItemWatchersQuery request, CancellationToken cancellationToken)
    {
        _ = await workItems.GetAsync(
                tenantContext.TenantId, request.WorkItemId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        var current = await watchers.ListByWorkItemAsync(
            tenantContext.TenantId, request.WorkItemId, cancellationToken);
        var isWatching = principal.UserId.HasValue && current.Any(watcher => watcher.UserId == principal.UserId);
        return new WorkItemWatchersDto(isWatching, current.Count);
    }
}
