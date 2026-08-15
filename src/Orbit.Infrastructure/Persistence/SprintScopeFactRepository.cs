using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Boards;

namespace Orbit.Infrastructure.Persistence;

internal sealed class SprintScopeFactRepository(OrbitDbContext dbContext) : ISprintScopeFactRepository
{
    public async Task AddAsync(SprintScopeFact fact, CancellationToken cancellationToken) =>
        await dbContext.SprintScopeFacts.AddAsync(fact, cancellationToken);

    public async Task<IReadOnlyList<SprintScopeFact>> ListBySprintAsync(
        Guid tenantId,
        Guid sprintId,
        CancellationToken cancellationToken) =>
        await dbContext.SprintScopeFacts
            .Where(fact => fact.TenantId == tenantId && fact.SprintId == sprintId)
            .OrderBy(fact => fact.OccurredAt)
            .ToListAsync(cancellationToken);
}
