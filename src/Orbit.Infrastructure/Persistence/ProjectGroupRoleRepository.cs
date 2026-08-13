using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Access;

namespace Orbit.Infrastructure.Persistence;

internal sealed class ProjectGroupRoleRepository(OrbitDbContext dbContext) : IProjectGroupRoleRepository
{
    public Task<ProjectGroupRoleAssignment?> GetAsync(
        Guid tenantId,
        Guid projectId,
        Guid groupId,
        CancellationToken cancellationToken) =>
        dbContext.ProjectGroupRoleAssignments.SingleOrDefaultAsync(
            assignment => assignment.TenantId == tenantId
                && assignment.ProjectId == projectId
                && assignment.GroupId == groupId,
            cancellationToken);

    public async Task AddAsync(
        ProjectGroupRoleAssignment assignment,
        CancellationToken cancellationToken) =>
        await dbContext.ProjectGroupRoleAssignments.AddAsync(assignment, cancellationToken);

    public async Task<IReadOnlyList<ProjectGroupRoleAssignment>> ListByProjectAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken) =>
        await dbContext.ProjectGroupRoleAssignments
            .AsNoTracking()
            .Where(assignment => assignment.TenantId == tenantId && assignment.ProjectId == projectId)
            .OrderBy(assignment => assignment.CreatedAt)
            .ToArrayAsync(cancellationToken);
}
