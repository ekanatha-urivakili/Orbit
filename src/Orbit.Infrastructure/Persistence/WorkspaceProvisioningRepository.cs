using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Access;
using Orbit.Domain.Configuration;
using Orbit.Domain.Identity;
using Orbit.Domain.Organizations;
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

    public async Task<OrganizationMembership?> GetOrganizationMembershipAsync(
        Guid workspaceTenantId, Guid userId, CancellationToken cancellationToken)
    {
        var organizationId = await dbContext.Workspaces
            .AsNoTracking()
            .Where(workspace => workspace.Id == workspaceTenantId)
            .Select(workspace => (Guid?)workspace.OrganizationId)
            .SingleOrDefaultAsync(cancellationToken);
        if (organizationId is null)
        {
            return null;
        }

        return await dbContext.OrganizationMemberships
            .AsNoTracking()
            .SingleOrDefaultAsync(
                membership => membership.OrganizationId == organizationId && membership.UserId == userId,
                cancellationToken);
    }

    public async Task AddAsync(
        Organization organization,
        Workspace workspace,
        OrganizationMembership organizationMembership,
        TenantMembership ownerMembership,
        Guid currentTenantId,
        CancellationToken cancellationToken)
    {
        await using var transaction = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.tenant_id', {workspace.Id.ToString()}, true)",
            cancellationToken);
        await dbContext.Organizations.AddAsync(organization, cancellationToken);
        await dbContext.OrganizationMemberships.AddAsync(organizationMembership, cancellationToken);
        await dbContext.Workspaces.AddAsync(workspace, cancellationToken);
        await dbContext.TenantMemberships.AddAsync(ownerMembership, cancellationToken);
        await dbContext.WorkItemTypeDefinitions.AddRangeAsync(
            WorkItemTypeDefinition.CreateSoftwareDefaults(workspace.Id, workspace.CreatedAt),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (currentTenantId != Guid.Empty)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT set_config('app.tenant_id', {currentTenantId.ToString()}, true)",
                cancellationToken);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task AddWorkspaceToOrganizationAsync(
        Workspace workspace,
        TenantMembership ownerMembership,
        Guid currentTenantId,
        CancellationToken cancellationToken)
    {
        await using var transaction = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.tenant_id', {workspace.Id.ToString()}, true)",
            cancellationToken);
        await dbContext.Workspaces.AddAsync(workspace, cancellationToken);
        await dbContext.TenantMemberships.AddAsync(ownerMembership, cancellationToken);
        await dbContext.WorkItemTypeDefinitions.AddRangeAsync(
            WorkItemTypeDefinition.CreateSoftwareDefaults(workspace.Id, workspace.CreatedAt),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (currentTenantId != Guid.Empty)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT set_config('app.tenant_id', {currentTenantId.ToString()}, true)",
                cancellationToken);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }
}
