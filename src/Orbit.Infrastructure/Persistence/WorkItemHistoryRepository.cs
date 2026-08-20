using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.WorkItems;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemHistoryRepository(OrbitDbContext dbContext) : IWorkItemHistoryRepository
{
    public async Task AddAsync(WorkItemHistoryEntry entry, CancellationToken cancellationToken) =>
        await dbContext.WorkItemHistoryEntries.AddAsync(entry, cancellationToken);

    public async Task<PagedResult<WorkItemHistoryEntry>> ListByWorkItemAsync(
        Guid tenantId,
        Guid workItemId,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = dbContext.WorkItemHistoryEntries
            .AsNoTracking()
            .Where(entry => entry.TenantId == tenantId && entry.WorkItemId == workItemId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(entry => entry.ChangedAt)
            .ThenBy(entry => entry.Id)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken);
        return new PagedResult<WorkItemHistoryEntry>(items, totalCount);
    }

    public async Task<IReadOnlyList<WorkItemHistoryEntry>> ListByWorkItemsAndFieldAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> workItemIds,
        string fieldName,
        CancellationToken cancellationToken)
    {
        if (workItemIds.Count == 0)
        {
            return [];
        }

        return await dbContext.WorkItemHistoryEntries
            .AsNoTracking()
            .Where(entry => entry.TenantId == tenantId
                && workItemIds.Contains(entry.WorkItemId)
                && entry.FieldName == fieldName)
            .OrderBy(entry => entry.ChangedAt)
            .ThenBy(entry => entry.Id)
            .ToArrayAsync(cancellationToken);
    }
}
