using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

public sealed record AddWorkItemVoteCommand(Guid WorkItemId) : ICommand<Unit>;

public sealed class AddWorkItemVoteValidator : AbstractValidator<AddWorkItemVoteCommand>
{
    public AddWorkItemVoteValidator() => RuleFor(command => command.WorkItemId).NotEmpty();
}

public sealed class AddWorkItemVoteHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IWorkItemRepository workItems,
    IWorkItemVoteRepository votes,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<AddWorkItemVoteCommand, Unit>
{
    public async Task<Unit> Handle(AddWorkItemVoteCommand request, CancellationToken cancellationToken)
    {
        var userId = PrincipalGuards.RequireUser(principal);
        _ = await workItems.GetAsync(
                tenantContext.TenantId, request.WorkItemId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        var existing = await votes.GetAsync(tenantContext.TenantId, request.WorkItemId, userId, cancellationToken);
        if (existing is not null)
        {
            return Unit.Value;
        }

        await votes.AddAsync(
            WorkItemVote.Create(tenantContext.TenantId, request.WorkItemId, userId, timeProvider.GetUtcNow()),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record RemoveWorkItemVoteCommand(Guid WorkItemId) : ICommand<Unit>;

public sealed class RemoveWorkItemVoteValidator : AbstractValidator<RemoveWorkItemVoteCommand>
{
    public RemoveWorkItemVoteValidator() => RuleFor(command => command.WorkItemId).NotEmpty();
}

public sealed class RemoveWorkItemVoteHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IWorkItemVoteRepository votes,
    IUnitOfWork unitOfWork) : IRequestHandler<RemoveWorkItemVoteCommand, Unit>
{
    public async Task<Unit> Handle(RemoveWorkItemVoteCommand request, CancellationToken cancellationToken)
    {
        var userId = PrincipalGuards.RequireUser(principal);
        var existing = await votes.GetAsync(tenantContext.TenantId, request.WorkItemId, userId, cancellationToken);
        if (existing is null)
        {
            return Unit.Value;
        }

        await votes.RemoveAsync(existing, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record WorkItemVotesDto(bool HasVoted, int Count);

public sealed record GetWorkItemVotesQuery(Guid WorkItemId) : IQuery<WorkItemVotesDto>;

public sealed class GetWorkItemVotesValidator : AbstractValidator<GetWorkItemVotesQuery>
{
    public GetWorkItemVotesValidator() => RuleFor(query => query.WorkItemId).NotEmpty();
}

public sealed class GetWorkItemVotesHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IWorkItemRepository workItems,
    IWorkItemVoteRepository votes) : IRequestHandler<GetWorkItemVotesQuery, WorkItemVotesDto>
{
    public async Task<WorkItemVotesDto> Handle(GetWorkItemVotesQuery request, CancellationToken cancellationToken)
    {
        _ = await workItems.GetAsync(
                tenantContext.TenantId, request.WorkItemId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        var current = await votes.ListByWorkItemAsync(tenantContext.TenantId, request.WorkItemId, cancellationToken);
        var hasVoted = principal.UserId.HasValue && current.Any(vote => vote.UserId == principal.UserId);
        return new WorkItemVotesDto(hasVoted, current.Count);
    }
}
