using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Directory;

namespace Orbit.Application.Directory;

public sealed record TeamDto(Guid Id, string Name, Guid CreatedByMembershipId, DateTimeOffset CreatedAt)
{
    public static TeamDto From(Team team) => new(team.Id, team.Name, team.CreatedByMembershipId, team.CreatedAt);
}

public sealed record TeamMembershipDto(Guid Id, Guid TeamId, Guid MembershipId, DateTimeOffset CreatedAt)
{
    public static TeamMembershipDto From(TeamMembership membership) =>
        new(membership.Id, membership.TeamId, membership.MembershipId, membership.CreatedAt);
}

public sealed record CreateTeamCommand(string Name) : ICommand<TeamDto>;

public sealed class CreateTeamValidator : AbstractValidator<CreateTeamCommand>
{
    public CreateTeamValidator() => RuleFor(command => command.Name).NotEmpty().Length(2, 120);
}

public sealed class CreateTeamHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    ITenantAuthorization authorization,
    ITeamRepository teams,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<CreateTeamCommand, TeamDto>
{
    public async Task<TeamDto> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        if (!authorization.CanManageTeams())
        {
            throw new AccessDeniedException("The current principal cannot manage teams.");
        }

        var team = Team.Create(tenantContext.TenantId, request.Name, principal.MembershipId, timeProvider.GetUtcNow());
        await teams.AddAsync(team, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TeamDto.From(team);
    }
}

public sealed record RenameTeamCommand(Guid TeamId, string Name) : ICommand<TeamDto>;

public sealed class RenameTeamValidator : AbstractValidator<RenameTeamCommand>
{
    public RenameTeamValidator() => RuleFor(command => command.Name).NotEmpty().Length(2, 120);
}

public sealed class RenameTeamHandler(
    ITenantContext tenantContext,
    ITenantAuthorization authorization,
    ITeamRepository teams,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<RenameTeamCommand, TeamDto>
{
    public async Task<TeamDto> Handle(RenameTeamCommand request, CancellationToken cancellationToken)
    {
        if (!authorization.CanManageTeams())
        {
            throw new AccessDeniedException("The current principal cannot manage teams.");
        }

        var team = await teams.GetAsync(tenantContext.TenantId, request.TeamId, cancellationToken)
            ?? throw new NotFoundException("Team was not found.");
        team.Rename(request.Name, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TeamDto.From(team);
    }
}

public sealed record ListTeamsQuery : IQuery<IReadOnlyList<TeamDto>>;

public sealed class ListTeamsHandler(ITenantContext tenantContext, ITeamRepository teams)
    : IRequestHandler<ListTeamsQuery, IReadOnlyList<TeamDto>>
{
    public async Task<IReadOnlyList<TeamDto>> Handle(ListTeamsQuery request, CancellationToken cancellationToken) =>
        (await teams.ListAsync(tenantContext.TenantId, cancellationToken)).Select(TeamDto.From).ToArray();
}

public sealed record ListTeamMembersQuery(Guid TeamId) : IQuery<IReadOnlyList<TeamMembershipDto>>;

public sealed class ListTeamMembersHandler(ITenantContext tenantContext, ITeamMembershipRepository teamMemberships)
    : IRequestHandler<ListTeamMembersQuery, IReadOnlyList<TeamMembershipDto>>
{
    public async Task<IReadOnlyList<TeamMembershipDto>> Handle(
        ListTeamMembersQuery request,
        CancellationToken cancellationToken) =>
        (await teamMemberships.ListByTeamAsync(tenantContext.TenantId, request.TeamId, cancellationToken))
            .Select(TeamMembershipDto.From)
            .ToArray();
}

public sealed record AddTeamMemberCommand(Guid TeamId, Guid MembershipId) : ICommand<TeamMembershipDto>;

public sealed class AddTeamMemberValidator : AbstractValidator<AddTeamMemberCommand>
{
    public AddTeamMemberValidator()
    {
        RuleFor(command => command.TeamId).NotEmpty();
        RuleFor(command => command.MembershipId).NotEmpty();
    }
}

public sealed class AddTeamMemberHandler(
    ITenantContext tenantContext,
    ITenantAuthorization authorization,
    ITeamRepository teams,
    ITeamMembershipRepository teamMemberships,
    ITenantMembershipRepository memberships,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<AddTeamMemberCommand, TeamMembershipDto>
{
    public async Task<TeamMembershipDto> Handle(AddTeamMemberCommand request, CancellationToken cancellationToken)
    {
        if (!authorization.CanManageTeams())
        {
            throw new AccessDeniedException("The current principal cannot manage teams.");
        }

        var tenantId = tenantContext.TenantId;
        _ = await teams.GetAsync(tenantId, request.TeamId, cancellationToken)
            ?? throw new NotFoundException("Team was not found.");
        _ = await memberships.GetActiveAsync(tenantId, request.MembershipId, cancellationToken)
            ?? throw new NotFoundException("Workspace membership was not found.");

        var existing = await teamMemberships.GetAsync(tenantId, request.TeamId, request.MembershipId, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException("This member already belongs to the team.");
        }

        var membership = TeamMembership.Create(tenantId, request.TeamId, request.MembershipId, timeProvider.GetUtcNow());
        await teamMemberships.AddAsync(membership, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TeamMembershipDto.From(membership);
    }
}

public sealed record RemoveTeamMemberCommand(Guid TeamId, Guid MembershipId) : ICommand<Unit>;

public sealed class RemoveTeamMemberHandler(
    ITenantContext tenantContext,
    ITenantAuthorization authorization,
    ITeamMembershipRepository teamMemberships,
    IUnitOfWork unitOfWork) : IRequestHandler<RemoveTeamMemberCommand, Unit>
{
    public async Task<Unit> Handle(RemoveTeamMemberCommand request, CancellationToken cancellationToken)
    {
        if (!authorization.CanManageTeams())
        {
            throw new AccessDeniedException("The current principal cannot manage teams.");
        }

        var membership = await teamMemberships.GetAsync(
            tenantContext.TenantId, request.TeamId, request.MembershipId, cancellationToken)
            ?? throw new NotFoundException("Team membership was not found.");
        await teamMemberships.RemoveAsync(membership, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
