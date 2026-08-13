using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Directory;

namespace Orbit.Infrastructure.Persistence;

internal sealed class TeamRepository(OrbitDbContext dbContext) : ITeamRepository
{
    public async Task AddAsync(Team team, CancellationToken cancellationToken) =>
        await dbContext.Teams.AddAsync(team, cancellationToken);

    public Task<Team?> GetAsync(Guid tenantId, Guid teamId, CancellationToken cancellationToken) =>
        dbContext.Teams.SingleOrDefaultAsync(
            team => team.TenantId == tenantId && team.Id == teamId,
            cancellationToken);

    public async Task<IReadOnlyList<Team>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await dbContext.Teams
            .AsNoTracking()
            .Where(team => team.TenantId == tenantId)
            .OrderBy(team => team.Name)
            .ToArrayAsync(cancellationToken);
}

internal sealed class TeamMembershipRepository(OrbitDbContext dbContext) : ITeamMembershipRepository
{
    public async Task AddAsync(TeamMembership membership, CancellationToken cancellationToken) =>
        await dbContext.TeamMemberships.AddAsync(membership, cancellationToken);

    public Task RemoveAsync(TeamMembership membership, CancellationToken cancellationToken)
    {
        dbContext.TeamMemberships.Remove(membership);
        return Task.CompletedTask;
    }

    public Task<TeamMembership?> GetAsync(
        Guid tenantId,
        Guid teamId,
        Guid membershipId,
        CancellationToken cancellationToken) =>
        dbContext.TeamMemberships.SingleOrDefaultAsync(
            membership => membership.TenantId == tenantId
                && membership.TeamId == teamId
                && membership.MembershipId == membershipId,
            cancellationToken);

    public async Task<IReadOnlyList<TeamMembership>> ListByTeamAsync(
        Guid tenantId,
        Guid teamId,
        CancellationToken cancellationToken) =>
        await dbContext.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.TenantId == tenantId && membership.TeamId == teamId)
            .OrderBy(membership => membership.CreatedAt)
            .ToArrayAsync(cancellationToken);
}
