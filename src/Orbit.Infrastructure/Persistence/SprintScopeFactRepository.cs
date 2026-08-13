using Orbit.Application.Abstractions;
using Orbit.Domain.Boards;

namespace Orbit.Infrastructure.Persistence;

internal sealed class SprintScopeFactRepository(OrbitDbContext dbContext) : ISprintScopeFactRepository
{
    public async Task AddAsync(SprintScopeFact fact, CancellationToken cancellationToken) =>
        await dbContext.SprintScopeFacts.AddAsync(fact, cancellationToken);
}
