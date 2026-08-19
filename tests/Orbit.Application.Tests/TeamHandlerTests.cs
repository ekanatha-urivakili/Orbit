using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.Directory;
using Orbit.Domain.Access;
using Orbit.Domain.Directory;

namespace Orbit.Application.Tests;

public sealed class TeamHandlerTests
{
    [Fact]
    public async Task CreateTeam_PersistsTeamOwnedByCurrentTenant()
    {
        var tenantId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var teams = new TeamRepositoryStub();
        var handler = new CreateTeamHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(membershipId),
            new AuthorizationStub(true),
            teams,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(new CreateTeamCommand("Platform Team"), CancellationToken.None);

        Assert.Equal("Platform Team", result.Name);
        Assert.Equal(tenantId, teams.Added!.TenantId);
        Assert.Equal(membershipId, teams.Added!.CreatedByMembershipId);
    }

    [Fact]
    public async Task CreateTeam_RejectsUnauthorizedPrincipal()
    {
        var teams = new TeamRepositoryStub();
        var handler = new CreateTeamHandler(
            new TenantContextStub(Guid.NewGuid()),
            new CurrentPrincipalStub(Guid.NewGuid()),
            new AuthorizationStub(false),
            teams,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(new CreateTeamCommand("Platform Team"), CancellationToken.None);

        await Assert.ThrowsAsync<AccessDeniedException>(action);
        Assert.Null(teams.Added);
    }

    [Fact]
    public async Task AddTeamMember_RejectsDuplicateMembership()
    {
        var tenantId = Guid.NewGuid();
        var team = Team.Create(tenantId, "Platform Team", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var membershipId = Guid.NewGuid();
        var teams = new TeamRepositoryStub { Existing = team };
        var teamMemberships = new TeamMembershipRepositoryStub
        {
            Existing = TeamMembership.Create(tenantId, team.Id, membershipId, DateTimeOffset.UtcNow)
        };
        var handler = new AddTeamMemberHandler(
            new TenantContextStub(tenantId),
            new AuthorizationStub(true),
            teams,
            teamMemberships,
            new TenantMembershipLookupStub(membershipId),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(new AddTeamMemberCommand(team.Id, membershipId), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(action);
    }

    [Fact]
    public async Task AddTeamMember_PersistsNewMembership()
    {
        var tenantId = Guid.NewGuid();
        var team = Team.Create(tenantId, "Platform Team", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var membershipId = Guid.NewGuid();
        var teams = new TeamRepositoryStub { Existing = team };
        var teamMemberships = new TeamMembershipRepositoryStub();
        var handler = new AddTeamMemberHandler(
            new TenantContextStub(tenantId),
            new AuthorizationStub(true),
            teams,
            teamMemberships,
            new TenantMembershipLookupStub(membershipId),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(new AddTeamMemberCommand(team.Id, membershipId), CancellationToken.None);

        Assert.Equal(membershipId, result.MembershipId);
        Assert.NotNull(teamMemberships.Added);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class CurrentPrincipalStub(Guid membershipId) : ICurrentPrincipal
    {
        public Guid? UserId => Guid.NewGuid();
        public Guid? SessionId => null;
        public Guid MembershipId => membershipId;
        public PrincipalType PrincipalType => PrincipalType.User;
        public TenantRole TenantRole => TenantRole.Owner;
        public MembershipTier MembershipTier => MembershipTier.Standard;
        public bool IsDevelopmentBypass => false;
    }

    private sealed class AuthorizationStub(bool allowed) : ITenantAuthorization
    {
        public bool CanCreateProject() => allowed;
        public bool CanCreateMembership(TenantRole role) => allowed;
        public bool CanManageTeams() => allowed;
    }

    private sealed class TeamRepositoryStub : ITeamRepository
    {
        public Team? Added { get; private set; }
        public Team? Existing { get; set; }

        public Task AddAsync(Team team, CancellationToken cancellationToken)
        {
            Added = team;
            return Task.CompletedTask;
        }

        public Task<Team?> GetAsync(Guid tenantId, Guid teamId, CancellationToken cancellationToken) =>
            Task.FromResult(Existing?.Id == teamId ? Existing : null);

        public Task<IReadOnlyList<Team>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Team>>(Existing is null ? [] : [Existing]);
    }

    private sealed class TeamMembershipRepositoryStub : ITeamMembershipRepository
    {
        public TeamMembership? Added { get; private set; }
        public TeamMembership? Removed { get; private set; }
        public TeamMembership? Existing { get; set; }

        public Task AddAsync(TeamMembership membership, CancellationToken cancellationToken)
        {
            Added = membership;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(TeamMembership membership, CancellationToken cancellationToken)
        {
            Removed = membership;
            return Task.CompletedTask;
        }

        public Task<TeamMembership?> GetAsync(
            Guid tenantId, Guid teamId, Guid membershipId, CancellationToken cancellationToken) =>
            Task.FromResult(
                Existing?.TeamId == teamId && Existing.MembershipId == membershipId ? Existing : null);

        public Task<IReadOnlyList<TeamMembership>> ListByTeamAsync(
            Guid tenantId, Guid teamId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TeamMembership>>(Existing is null ? [] : [Existing]);
    }

    private sealed class TenantMembershipLookupStub(Guid membershipId) : ITenantMembershipRepository
    {
        public Task AddAsync(TenantMembership membership, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<TenantMembership?> GetActiveAsync(
            Guid tenantId, string issuer, string subject, CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<TenantMembership?> GetActiveByUserAsync(
            Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<TenantMembership?> GetActiveAsync(
            Guid tenantId, Guid requestedMembershipId, CancellationToken cancellationToken) =>
            Task.FromResult(
                requestedMembershipId == membershipId
                    ? TenantMembership.CreateForUser(tenantId, Guid.NewGuid(), TenantRole.Member, DateTimeOffset.UtcNow)
                    : null);

        public Task<TenantMembership?> GetOwnerAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<IReadOnlyList<TenantMembership>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<IReadOnlyList<TenantMembership>> ListByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> membershipIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<IReadOnlyList<Guid>> ListActiveUserIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
