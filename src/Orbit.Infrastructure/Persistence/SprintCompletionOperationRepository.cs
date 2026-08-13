using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Boards;

namespace Orbit.Infrastructure.Persistence;

internal sealed class SprintCompletionOperationRepository(OrbitDbContext dbContext) : ISprintCompletionOperationRepository
{
    public async Task AddAsync(SprintCompletionOperation operation, CancellationToken cancellationToken) =>
        await dbContext.SprintCompletionOperations.AddAsync(operation, cancellationToken);

    public Task<SprintCompletionOperation?> GetAsync(
        Guid tenantId, Guid sprintId, CancellationToken cancellationToken) =>
        dbContext.SprintCompletionOperations.SingleOrDefaultAsync(
            operation => operation.TenantId == tenantId && operation.SprintId == sprintId,
            cancellationToken);
}
