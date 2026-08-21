using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Access;

namespace Orbit.Infrastructure.Persistence;

internal sealed class RoleRepository(OrbitDbContext dbContext) : IRoleRepository
{
    public async Task AddAsync(Role role, CancellationToken cancellationToken) =>
        await dbContext.Roles.AddAsync(role, cancellationToken);

    public Task<Role?> GetAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken) =>
        dbContext.Roles.Include(role => role.Permissions).SingleOrDefaultAsync(
            role => role.TenantId == tenantId && role.Id == roleId,
            cancellationToken);

    public Task<Role?> GetByNameAsync(Guid tenantId, string name, CancellationToken cancellationToken) =>
        dbContext.Roles.Include(role => role.Permissions).SingleOrDefaultAsync(
            role => role.TenantId == tenantId && role.Name == name,
            cancellationToken);

    public async Task<IReadOnlyList<Role>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await dbContext.Roles
            .Include(role => role.Permissions)
            .Where(role => role.TenantId == tenantId)
            .OrderBy(role => role.CreatedAt)
            .ToArrayAsync(cancellationToken);

    public async Task<bool> HasAssignmentsAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken) =>
        await dbContext.ProjectRoleAssignments
            .AnyAsync(assignment => assignment.TenantId == tenantId && assignment.RoleId == roleId, cancellationToken)
        || await dbContext.ProjectGroupRoleAssignments
            .AnyAsync(assignment => assignment.TenantId == tenantId && assignment.RoleId == roleId, cancellationToken);

    public Task RemoveAsync(Role role, CancellationToken cancellationToken)
    {
        dbContext.Roles.Remove(role);
        return Task.CompletedTask;
    }
}
