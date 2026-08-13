using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Boards;
using Orbit.Domain.Choices;

namespace Orbit.Infrastructure.Persistence;

internal sealed class SprintRepository(OrbitDbContext dbContext) : ISprintRepository
{
    public async Task AddAsync(Sprint sprint, CancellationToken cancellationToken) =>
        await dbContext.Sprints.AddAsync(sprint, cancellationToken);

    public Task<Sprint?> GetAsync(Guid tenantId, Guid sprintId, CancellationToken cancellationToken) =>
        dbContext.Sprints.SingleOrDefaultAsync(
            sprint => sprint.TenantId == tenantId && sprint.Id == sprintId,
            cancellationToken);

    public Task<Sprint?> GetActiveAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Sprints.SingleOrDefaultAsync(
            sprint => sprint.TenantId == tenantId
                && sprint.ProjectId == projectId
                && sprint.State == SprintState.Active,
            cancellationToken);

    public async Task<IReadOnlyList<Sprint>> ListByProjectAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken) =>
        await dbContext.Sprints
            .Where(sprint => sprint.TenantId == tenantId && sprint.ProjectId == projectId)
            .OrderBy(sprint => sprint.CreatedAt)
            .ToListAsync(cancellationToken);
}
