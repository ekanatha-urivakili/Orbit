using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Configuration;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemStatusRepository(OrbitDbContext dbContext) : IWorkItemStatusRepository
{
    public async Task AddAsync(WorkItemStatusDefinition definition, CancellationToken cancellationToken) =>
        await dbContext.WorkItemStatusDefinitions.AddAsync(definition, cancellationToken);

    public async Task AddRangeAsync(
        IReadOnlyCollection<WorkItemStatusDefinition> definitions, CancellationToken cancellationToken) =>
        await dbContext.WorkItemStatusDefinitions.AddRangeAsync(definitions, cancellationToken);

    public Task<WorkItemStatusDefinition?> GetAsync(
        Guid tenantId, Guid projectId, Guid statusId, CancellationToken cancellationToken) =>
        dbContext.WorkItemStatusDefinitions.SingleOrDefaultAsync(
            definition => definition.TenantId == tenantId
                && definition.ProjectId == projectId
                && definition.Id == statusId,
            cancellationToken);

    public async Task<IReadOnlyList<WorkItemStatusDefinition>> ListByProjectAsync(
        Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.WorkItemStatusDefinitions
            .Where(definition => definition.TenantId == tenantId && definition.ProjectId == projectId)
            .OrderBy(definition => definition.Order)
            .ThenBy(definition => definition.Name)
            .ToArrayAsync(cancellationToken);

    public async Task<WorkItemStatusDefinition?> GetDefaultAsync(
        Guid tenantId, Guid projectId, CancellationToken cancellationToken)
    {
        var flagged = await dbContext.WorkItemStatusDefinitions.SingleOrDefaultAsync(
            definition => definition.TenantId == tenantId && definition.ProjectId == projectId && definition.IsDefault,
            cancellationToken);
        if (flagged is not null)
        {
            return flagged;
        }

        return await dbContext.WorkItemStatusDefinitions
            .Where(definition => definition.TenantId == tenantId && definition.ProjectId == projectId)
            .OrderBy(definition => definition.Order)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> IsInUseAsync(
        Guid tenantId, Guid projectId, Guid statusId, string statusKey, CancellationToken cancellationToken)
    {
        var referencedByWorkItem = await dbContext.WorkItems.AnyAsync(
            workItem => workItem.TenantId == tenantId
                && workItem.ProjectId == projectId
                && workItem.StatusId == statusId,
            cancellationToken);
        if (referencedByWorkItem)
        {
            return true;
        }

        var board = await dbContext.Boards.SingleOrDefaultAsync(
            b => b.TenantId == tenantId && b.ProjectId == projectId, cancellationToken);
        if (board is not null && board.Columns.Any(column => column.StatusId == statusId))
        {
            return true;
        }

        // History entries record the status *key* (stable across renames, see ChangeWorkItemStatusHandler),
        // so this is the only reliable way to tell whether reports have ever depended on this status.
        return await dbContext.WorkItemHistoryEntries
            .Where(entry => entry.TenantId == tenantId
                && entry.FieldName == "Status"
                && (entry.OldValue == statusKey || entry.NewValue == statusKey))
            .Join(
                dbContext.WorkItems.Where(workItem => workItem.TenantId == tenantId && workItem.ProjectId == projectId),
                entry => entry.WorkItemId,
                workItem => workItem.Id,
                (entry, workItem) => entry.Id)
            .AnyAsync(cancellationToken);
    }

    public async Task RemoveAsync(WorkItemStatusDefinition definition, CancellationToken cancellationToken)
    {
        dbContext.WorkItemStatusDefinitions.Remove(definition);
        await Task.CompletedTask;
    }
}
