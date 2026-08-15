using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Access;
using Orbit.Domain.Identity;
using Orbit.Domain.Workspaces;

namespace Orbit.Infrastructure.Persistence;

internal sealed class AuthenticationRepository(OrbitDbContext dbContext) : IAuthenticationRepository
{
    public Task<UserAccount?> GetUserAccountAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.UserAccounts.SingleOrDefaultAsync(account => account.Id == userId, cancellationToken);

    public Task<UserAccount?> GetUserAccountByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken) =>
        dbContext.UserAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(account => account.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<LocalCredential?> GetLocalCredentialAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.LocalCredentials
            .AsNoTracking()
            .SingleOrDefaultAsync(credential => credential.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<TenantMembership>> ListActiveMembershipsByUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Login/refresh run before a tenant is selected, while /me/workspaces calls this inside the
        // request's existing tenant transaction. This bypasses the per-request EF query filter and - because
        // tenant_memberships carries FORCE ROW LEVEL SECURITY - identifies the user transaction-locally
        // that identifies the caller to the database via app.principal_user_id. The
        // tenant_memberships_self_lookup RLS policy permits a SELECT of a user's own membership rows
        // under that GUC even though no app.tenant_id is set; every other table/column stays governed
        // by the tenant-scoped policy. Without this, the query would run under either a bypassing
        // (e.g. superuser) role - masking the gap in development - or, once a real non-superuser
        // runtime role is enforced, silently return zero rows and fail discovery.
        await using var transaction = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.principal_user_id', {userId.ToString()}, true)",
            cancellationToken);

        var memberships = await dbContext.TenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(membership => membership.UserId == userId && membership.IsActive)
            .OrderBy(membership => membership.CreatedAt)
            .ToArrayAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return memberships;
    }

    public Task<Workspace?> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.Workspaces.AsNoTracking().SingleOrDefaultAsync(workspace => workspace.Id == tenantId, cancellationToken);

    public async Task<IReadOnlyList<Workspace>> GetWorkspacesAsync(
        IReadOnlyCollection<Guid> tenantIds,
        CancellationToken cancellationToken) =>
        await dbContext.Workspaces
            .AsNoTracking()
            .Where(workspace => tenantIds.Contains(workspace.Id))
            .ToListAsync(cancellationToken);

    public async Task AddRefreshSessionAsync(RefreshSession session, CancellationToken cancellationToken) =>
        await dbContext.RefreshSessions.AddAsync(session, cancellationToken);

    public Task<RefreshSession?> GetRefreshSessionByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken) =>
        dbContext.RefreshSessions.SingleOrDefaultAsync(session => session.TokenHash == tokenHash, cancellationToken);

    public Task<RefreshSession?> GetActiveSessionAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.RefreshSessions.SingleOrDefaultAsync(
            session => session.Id == sessionId
                && session.UserId == userId
                && session.Status == RefreshSessionStatus.Active,
            cancellationToken);

    public async Task<IReadOnlyList<RefreshSession>> ListActiveSessionsByUserAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await dbContext.RefreshSessions
            .Where(session => session.UserId == userId && session.Status == RefreshSessionStatus.Active)
            .ToArrayAsync(cancellationToken);

    public async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var sessions = await dbContext.RefreshSessions
            .Where(session => session.FamilyId == familyId && session.Status == RefreshSessionStatus.Active)
            .ToArrayAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.Revoke(now);
        }
    }

    public async Task AddExternalIdentityAsync(ExternalIdentity identity, CancellationToken cancellationToken) =>
        await dbContext.ExternalIdentities.AddAsync(identity, cancellationToken);

    public Task<ExternalIdentity?> GetExternalIdentityAsync(
        string issuer, string subject, CancellationToken cancellationToken) =>
        dbContext.ExternalIdentities
            .AsNoTracking()
            .SingleOrDefaultAsync(identity => identity.Issuer == issuer && identity.Subject == subject, cancellationToken);

    public async Task<IReadOnlyList<ExternalIdentity>> ListExternalIdentitiesByUserAsync(
        Guid userId, CancellationToken cancellationToken) =>
        await dbContext.ExternalIdentities
            .AsNoTracking()
            .Where(identity => identity.UserId == userId)
            .OrderBy(identity => identity.CreatedAt)
            .ToArrayAsync(cancellationToken);

    public Task<ExternalIdentity?> GetExternalIdentityAsync(
        Guid id, Guid userId, CancellationToken cancellationToken) =>
        dbContext.ExternalIdentities.SingleOrDefaultAsync(
            identity => identity.Id == id && identity.UserId == userId, cancellationToken);

    public Task RemoveExternalIdentityAsync(ExternalIdentity identity, CancellationToken cancellationToken)
    {
        dbContext.ExternalIdentities.Remove(identity);
        return Task.CompletedTask;
    }

    public async Task AddPasswordResetTokenAsync(PasswordResetToken token, CancellationToken cancellationToken) =>
        await dbContext.PasswordResetTokens.AddAsync(token, cancellationToken);

    public Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken) =>
        dbContext.PasswordResetTokens.SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public async Task RevokeActivePasswordResetTokensForUserAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var tokens = await dbContext.PasswordResetTokens
            .Where(token => token.UserId == userId && token.Status == PasswordResetTokenStatus.Active)
            .ToArrayAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.Revoke(now);
        }
    }

    public Task UpdateLocalCredentialAsync(LocalCredential credential, CancellationToken cancellationToken)
    {
        dbContext.LocalCredentials.Update(credential);
        return Task.CompletedTask;
    }

    public async Task AddServiceAccountCredentialAsync(
        ServiceAccountCredential credential,
        CancellationToken cancellationToken) =>
        await dbContext.ServiceAccountCredentials.AddAsync(credential, cancellationToken);

    public Task<ServiceAccountCredential?> GetActiveServiceAccountCredentialByClientIdAsync(
        Guid clientId,
        CancellationToken cancellationToken) =>
        dbContext.ServiceAccountCredentials.SingleOrDefaultAsync(
            credential => credential.ClientId == clientId && credential.RevokedAt == null, cancellationToken);

    public async Task<IReadOnlyList<ServiceAccountCredential>> ListActiveServiceAccountCredentialsByMembershipAsync(
        Guid membershipId,
        CancellationToken cancellationToken) =>
        await dbContext.ServiceAccountCredentials
            .Where(credential => credential.MembershipId == membershipId && credential.RevokedAt == null)
            .ToArrayAsync(cancellationToken);

    public async Task<TenantMembership?> GetActiveServiceAccountMembershipAsync(
        Guid tenantId,
        Guid membershipId,
        CancellationToken cancellationToken)
    {
        // Pre-auth token issuance: there is no ambient tenant transaction/context yet, and
        // TenantMemberships carries FORCE ROW LEVEL SECURITY, so app.tenant_id must be set from the
        // already-verified credential's own tenant id before this query can see anything - same
        // technique as ListActiveMembershipsByUserAsync's app.principal_user_id self-lookup above.
        await using var transaction = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.tenant_id', {tenantId.ToString()}, true)",
            cancellationToken);

        var membership = await dbContext.TenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.TenantId == tenantId
                    && value.Id == membershipId
                    && value.IsActive
                    && value.PrincipalType == PrincipalType.ServiceAccount,
                cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return membership;
    }
}
