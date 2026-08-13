using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Access;

namespace Orbit.Infrastructure.Persistence;

internal sealed class ProjectRoleRepository(OrbitDbContext dbContext) : IProjectRoleRepository
{
    public Task<ProjectRoleAssignment?> GetAsync(
        Guid tenantId,
        Guid projectId,
        Guid membershipId,
        CancellationToken cancellationToken) =>
        dbContext.ProjectRoleAssignments.SingleOrDefaultAsync(
            assignment => assignment.TenantId == tenantId
                && assignment.ProjectId == projectId
                && assignment.MembershipId == membershipId,
            cancellationToken);

    public async Task AddAsync(
        ProjectRoleAssignment assignment,
        CancellationToken cancellationToken) =>
        await dbContext.ProjectRoleAssignments.AddAsync(assignment, cancellationToken);

    public async Task<IReadOnlyList<ProjectRoleAssignment>> ListByProjectAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken) =>
        await dbContext.ProjectRoleAssignments
            .AsNoTracking()
            .Where(assignment => assignment.TenantId == tenantId && assignment.ProjectId == projectId)
            .OrderBy(assignment => assignment.CreatedAt)
            .ToArrayAsync(cancellationToken);
}
