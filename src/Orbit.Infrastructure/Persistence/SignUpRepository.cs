using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Access;
using Orbit.Domain.Configuration;
using Orbit.Domain.Identity;
using Orbit.Domain.Organizations;
using Orbit.Domain.Workspaces;

namespace Orbit.Infrastructure.Persistence;

internal sealed class SignUpRepository(OrbitDbContext dbContext) : ISignUpRepository
{
    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.UserAccounts.AsNoTracking().AnyAsync(
            account => account.NormalizedEmail == normalizedEmail,
            cancellationToken);

    public async Task AddAsync(
        UserAccount account,
        LocalCredential credential,
        Organization organization,
        Workspace workspace,
        OrganizationMembership organizationMembership,
        TenantMembership ownerMembership,
        RefreshSession refreshSession,
        CancellationToken cancellationToken)
    {
        // No ambient tenant transaction exists yet (unauthenticated request), so app.tenant_id must
        // be set from the new workspace's own id before inserting rows that carry FORCE ROW LEVEL
        // SECURITY (tenant_memberships, work_item_type_definitions) - same technique as
        // BootstrapRepository/WorkspaceProvisioningRepository.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.tenant_id', {workspace.Id.ToString()}, true)",
            cancellationToken);

        await dbContext.UserAccounts.AddAsync(account, cancellationToken);
        await dbContext.LocalCredentials.AddAsync(credential, cancellationToken);
        await dbContext.Organizations.AddAsync(organization, cancellationToken);
        await dbContext.OrganizationMemberships.AddAsync(organizationMembership, cancellationToken);
        await dbContext.Workspaces.AddAsync(workspace, cancellationToken);
        await dbContext.TenantMemberships.AddAsync(ownerMembership, cancellationToken);
        await dbContext.WorkItemTypeDefinitions.AddRangeAsync(
            WorkItemTypeDefinition.CreateSoftwareDefaults(workspace.Id, workspace.CreatedAt),
            cancellationToken);
        await dbContext.RefreshSessions.AddAsync(refreshSession, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
