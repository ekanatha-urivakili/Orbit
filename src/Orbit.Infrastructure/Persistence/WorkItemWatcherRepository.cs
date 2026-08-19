using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.WorkItems;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemWatcherRepository(OrbitDbContext dbContext) : IWorkItemWatcherRepository
{
    // Raw insert (not tracked-add + SaveChanges) so concurrent duplicate watch requests are
    // idempotent instead of one losing to ux_work_item_watchers_tenant_item_user with an
    // unhandled unique-constraint violation.
    public async Task AddAsync(WorkItemWatcher watcher, CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO work_item_watchers (id, tenant_id, work_item_id, user_id, created_at)
            VALUES ({watcher.Id}, {watcher.TenantId}, {watcher.WorkItemId}, {watcher.UserId}, {watcher.CreatedAt})
            ON CONFLICT (tenant_id, work_item_id, user_id) DO NOTHING
            """,
            cancellationToken);

    public Task<WorkItemWatcher?> GetAsync(
        Guid tenantId, Guid workItemId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.WorkItemWatchers
            .SingleOrDefaultAsync(
                watcher => watcher.TenantId == tenantId
                    && watcher.WorkItemId == workItemId
                    && watcher.UserId == userId,
                cancellationToken);

    public async Task<IReadOnlyList<WorkItemWatcher>> ListByWorkItemAsync(
        Guid tenantId, Guid workItemId, CancellationToken cancellationToken) =>
        await dbContext.WorkItemWatchers
            .AsNoTracking()
            .Where(watcher => watcher.TenantId == tenantId && watcher.WorkItemId == workItemId)
            .ToArrayAsync(cancellationToken);

    public Task RemoveAsync(WorkItemWatcher watcher, CancellationToken cancellationToken)
    {
        dbContext.WorkItemWatchers.Remove(watcher);
        return Task.CompletedTask;
    }
}
