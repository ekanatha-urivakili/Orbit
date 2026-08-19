using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.WorkItems;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemHistoryRepository(OrbitDbContext dbContext) : IWorkItemHistoryRepository
{
    public async Task AddAsync(WorkItemHistoryEntry entry, CancellationToken cancellationToken) =>
        await dbContext.WorkItemHistoryEntries.AddAsync(entry, cancellationToken);

    public async Task<IReadOnlyList<WorkItemHistoryEntry>> ListByWorkItemAsync(
        Guid tenantId,
        Guid workItemId,
        CancellationToken cancellationToken) =>
        await dbContext.WorkItemHistoryEntries
            .AsNoTracking()
            .Where(entry => entry.TenantId == tenantId && entry.WorkItemId == workItemId)
            .OrderBy(entry => entry.ChangedAt)
            .ThenBy(entry => entry.Id)
            .ToArrayAsync(cancellationToken);
}
