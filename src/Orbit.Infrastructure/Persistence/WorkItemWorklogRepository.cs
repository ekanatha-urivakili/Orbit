using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.WorkItems;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemWorklogRepository(OrbitDbContext dbContext) : IWorkItemWorklogRepository
{
    public async Task AddAsync(WorkItemWorklog worklog, CancellationToken cancellationToken) =>
        await dbContext.WorkItemWorklogs.AddAsync(worklog, cancellationToken);

    public Task<WorkItemWorklog?> GetAsync(Guid tenantId, Guid worklogId, CancellationToken cancellationToken) =>
        dbContext.WorkItemWorklogs
            .SingleOrDefaultAsync(worklog => worklog.TenantId == tenantId && worklog.Id == worklogId, cancellationToken);

    public async Task<PagedResult<WorkItemWorklog>> ListByWorkItemAsync(
        Guid tenantId, Guid workItemId, int skip, int take, CancellationToken cancellationToken)
    {
        var query = dbContext.WorkItemWorklogs
            .AsNoTracking()
            .Where(worklog => worklog.TenantId == tenantId && worklog.WorkItemId == workItemId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(worklog => worklog.WorkDate)
            .ThenByDescending(worklog => worklog.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken);
        return new PagedResult<WorkItemWorklog>(items, totalCount);
    }

    public Task RemoveAsync(WorkItemWorklog worklog, CancellationToken cancellationToken)
    {
        dbContext.WorkItemWorklogs.Remove(worklog);
        return Task.CompletedTask;
    }
}
