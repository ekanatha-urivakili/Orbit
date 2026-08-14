using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Access;
using Orbit.Domain.Configuration;
using Orbit.Domain.Identity;
using Orbit.Domain.Workspaces;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkspaceProvisioningRepository(OrbitDbContext dbContext)
    : IWorkspaceProvisioningRepository
{
    public Task<bool> IsSiteSuperAdministratorAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.SiteRoleAssignments
            .AsNoTracking()
            .AnyAsync(
                assignment => assignment.UserId == userId
                    && assignment.Role == SiteRole.SuperAdministrator,
                cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        dbContext.Workspaces.AsNoTracking().AnyAsync(
            workspace => workspace.Slug == slug,
            cancellationToken);

    public async Task AddAsync(
        Workspace workspace,
        TenantMembership ownerMembership,
        Guid currentTenantId,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Workspace provisioning requires an ambient request transaction.");
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.tenant_id', {workspace.Id.ToString()}, true)",
            cancellationToken);
        await dbContext.Workspaces.AddAsync(workspace, cancellationToken);
        await dbContext.TenantMemberships.AddAsync(ownerMembership, cancellationToken);
        await dbContext.WorkItemTypeDefinitions.AddRangeAsync(
            WorkItemTypeDefinition.CreateSoftwareDefaults(workspace.Id, workspace.CreatedAt),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.tenant_id', {currentTenantId.ToString()}, true)",
            cancellationToken);
    }
}
