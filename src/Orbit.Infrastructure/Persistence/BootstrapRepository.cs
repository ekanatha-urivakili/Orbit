using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Access;
using Orbit.Domain.Configuration;
using Orbit.Domain.Identity;
using Orbit.Domain.Organizations;
using Orbit.Domain.Workspaces;

namespace Orbit.Infrastructure.Persistence;

internal sealed class BootstrapRepository(OrbitDbContext dbContext) : IBootstrapRepository
{
    private const string AcquireBootstrapLockSql =
        "SELECT pg_advisory_xact_lock(5226642927965431097)";

    public async Task<bool> IsInitializationRequiredAsync(CancellationToken cancellationToken) =>
        !await dbContext.SiteRoleAssignments
            .AsNoTracking()
            .AnyAsync(
                assignment => assignment.Role == SiteRole.SuperAdministrator,
                cancellationToken);

    public async Task<bool> TryInitializeAsync(
        UserAccount account,
        LocalCredential credential,
        SiteRoleAssignment siteRole,
        Organization organization,
        Workspace workspace,
        OrganizationMembership organizationMembership,
        TenantMembership ownerMembership,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            AcquireBootstrapLockSql,
            cancellationToken);

        var initialized = await dbContext.SiteRoleAssignments
            .AsNoTracking()
            .AnyAsync(
                assignment => assignment.Role == SiteRole.SuperAdministrator,
                cancellationToken);
        if (initialized)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.tenant_id', {workspace.Id.ToString()}, true)",
            cancellationToken);

        await dbContext.UserAccounts.AddAsync(account, cancellationToken);
        await dbContext.LocalCredentials.AddAsync(credential, cancellationToken);
        await dbContext.SiteRoleAssignments.AddAsync(siteRole, cancellationToken);
        await dbContext.Organizations.AddAsync(organization, cancellationToken);
        await dbContext.OrganizationMemberships.AddAsync(organizationMembership, cancellationToken);
        await dbContext.Workspaces.AddAsync(workspace, cancellationToken);
        await dbContext.TenantMemberships.AddAsync(ownerMembership, cancellationToken);
        await dbContext.WorkItemTypeDefinitions.AddRangeAsync(
            WorkItemTypeDefinition.CreateSoftwareDefaults(workspace.Id, workspace.CreatedAt),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
