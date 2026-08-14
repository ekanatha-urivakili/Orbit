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
        // Login/refresh run before a tenant is selected (no TenantTransactionMiddleware transaction
        // is open), so this both bypasses the per-request EF query filter and - because
        // tenant_memberships carries FORCE ROW LEVEL SECURITY - explicitly opens a short transaction
        // that identifies the caller to the database via app.principal_user_id. The
        // tenant_memberships_self_lookup RLS policy permits a SELECT of a user's own membership rows
        // under that GUC even though no app.tenant_id is set; every other table/column stays governed
        // by the tenant-scoped policy. Without this, the query would run under either a bypassing
        // (e.g. superuser) role - masking the gap in development - or, once a real non-superuser
        // runtime role is enforced, silently return zero rows and fail every login.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.principal_user_id', {userId.ToString()}, true)",
            cancellationToken);

        var memberships = await dbContext.TenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(membership => membership.UserId == userId && membership.IsActive)
            .OrderBy(membership => membership.CreatedAt)
            .ToArrayAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return memberships;
    }

    public Task<Workspace?> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.Workspaces.AsNoTracking().SingleOrDefaultAsync(workspace => workspace.Id == tenantId, cancellationToken);

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
}
