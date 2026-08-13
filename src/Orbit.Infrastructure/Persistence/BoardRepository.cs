using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Boards;

namespace Orbit.Infrastructure.Persistence;

internal sealed class BoardRepository(OrbitDbContext dbContext) : IBoardRepository
{
    public async Task AddAsync(Board board, CancellationToken cancellationToken) =>
        await dbContext.Boards.AddAsync(board, cancellationToken);

    public Task<Board?> GetAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Boards.SingleOrDefaultAsync(
            board => board.TenantId == tenantId && board.ProjectId == projectId,
            cancellationToken);
}
