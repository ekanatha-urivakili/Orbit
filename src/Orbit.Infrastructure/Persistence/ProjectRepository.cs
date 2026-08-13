using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Projects;

namespace Orbit.Infrastructure.Persistence;

internal sealed class ProjectRepository(
    OrbitDbContext dbContext,
    ICurrentPrincipal principal) : IProjectRepository
{
    public async Task AddAsync(Project project, CancellationToken cancellationToken) =>
        await dbContext.Projects.AddAsync(project, cancellationToken);

    public Task<Project?> GetAsync(
        Guid tenantId,
        Guid projectId,
        ProjectPermission permission,
        CancellationToken cancellationToken) =>
        PermittedProjects(tenantId, permission).SingleOrDefaultAsync(
            project => project.Id == projectId,
            cancellationToken);

    public async Task<PagedResult<Project>> ListAsync(
        Guid tenantId,
        ProjectPermission permission,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = PermittedProjects(tenantId, permission).AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(project => project.Key)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken);
        return new PagedResult<Project>(items, totalCount);
    }

    private IQueryable<Project> PermittedProjects(Guid tenantId, ProjectPermission permission)
        => ProjectAccessQuery.PermittedProjects(dbContext, principal, tenantId, permission);
}
