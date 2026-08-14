using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Access;
using Orbit.Domain.Directory;
using Orbit.Domain.Identity;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkspaceInvitationRepository(OrbitDbContext dbContext) : IWorkspaceInvitationRepository
{
    public async Task AddAsync(WorkspaceInvitation invitation, CancellationToken cancellationToken) =>
        await dbContext.WorkspaceInvitations.AddAsync(invitation, cancellationToken);

    public Task<WorkspaceInvitation?> GetActiveByEmailAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken) =>
        dbContext.WorkspaceInvitations.SingleOrDefaultAsync(
            invitation => invitation.TenantId == tenantId
                && invitation.NormalizedEmail == normalizedEmail
                && invitation.Status == WorkspaceInvitationStatus.Active,
            cancellationToken);

    public Task<WorkspaceInvitation?> GetByTokenHashAsync(
        Guid tenantId,
        string tokenHash,
        CancellationToken cancellationToken) =>
        dbContext.WorkspaceInvitations.SingleOrDefaultAsync(
            invitation => invitation.TenantId == tenantId && invitation.TokenHash == tokenHash,
            cancellationToken);

    public Task<WorkspaceInvitation?> GetAsync(
        Guid tenantId,
        Guid invitationId,
        CancellationToken cancellationToken) =>
        dbContext.WorkspaceInvitations.SingleOrDefaultAsync(
            invitation => invitation.TenantId == tenantId && invitation.Id == invitationId,
            cancellationToken);

    public async Task<IReadOnlyList<WorkspaceInvitation>> ListAsync(
        Guid tenantId,
        string? emailSearch,
        WorkspaceInvitationStatus? status,
        CancellationToken cancellationToken)
    {
        var query = dbContext.WorkspaceInvitations
            .AsNoTracking()
            .Where(invitation => invitation.TenantId == tenantId);
        if (emailSearch is not null)
        {
            query = query.Where(invitation => invitation.NormalizedEmail.Contains(emailSearch));
        }

        if (status is { } requestedStatus)
        {
            query = query.Where(invitation => invitation.Status == requestedStatus);
        }

        return await query
            .OrderByDescending(invitation => invitation.CreatedAt)
            .ToArrayAsync(cancellationToken);
    }

    public Task<UserAccount?> GetUserAccountByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken) =>
        dbContext.UserAccounts.SingleOrDefaultAsync(
            account => account.NormalizedEmail == normalizedEmail,
            cancellationToken);

    public Task<LocalCredential?> GetUserAccountCredentialAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.LocalCredentials.SingleOrDefaultAsync(
            credential => credential.UserId == userId,
            cancellationToken);

    public Task<TenantMembership?> GetMembershipByUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.TenantMemberships.SingleOrDefaultAsync(
            membership => membership.TenantId == tenantId && membership.UserId == userId,
            cancellationToken);

    public Task<TeamMembership?> GetTeamMembershipAsync(
        Guid tenantId,
        Guid teamId,
        Guid membershipId,
        CancellationToken cancellationToken) =>
        dbContext.TeamMemberships.SingleOrDefaultAsync(
            membership => membership.TenantId == tenantId
                && membership.TeamId == teamId
                && membership.MembershipId == membershipId,
            cancellationToken);

    public async Task AddUserAccountAsync(UserAccount account, CancellationToken cancellationToken) =>
        await dbContext.UserAccounts.AddAsync(account, cancellationToken);

    public async Task AddLocalCredentialAsync(LocalCredential credential, CancellationToken cancellationToken) =>
        await dbContext.LocalCredentials.AddAsync(credential, cancellationToken);

    public async Task AddTenantMembershipAsync(TenantMembership membership, CancellationToken cancellationToken) =>
        await dbContext.TenantMemberships.AddAsync(membership, cancellationToken);

    public async Task AddTeamMembershipAsync(TeamMembership membership, CancellationToken cancellationToken) =>
        await dbContext.TeamMemberships.AddAsync(membership, cancellationToken);
}
