using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.WorkItems;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemWorklogRepository(OrbitDbContext dbContext) : IWorkItemWorklogRepository
{
    public async Task AddAsync(WorkItemWorklog worklog, CancellationToken cancellationToken) =>
        await dbContext.WorkItemWorklogs.AddAsync(worklog, cancellationToken);

    public Task<WorkItemWorklog?> GetAsync(Guid tenantId, Guid worklogId, CancellationToken cancellationToken) =>
        dbContext.WorkItemWorklogs
            .SingleOrDefaultAsync(worklog => worklog.TenantId == tenantId && worklog.Id == worklogId, cancellationToken);

    public async Task<IReadOnlyList<WorkItemWorklog>> ListByWorkItemAsync(
        Guid tenantId, Guid workItemId, CancellationToken cancellationToken) =>
        await dbContext.WorkItemWorklogs
            .AsNoTracking()
            .Where(worklog => worklog.TenantId == tenantId && worklog.WorkItemId == workItemId)
            .OrderByDescending(worklog => worklog.WorkDate)
            .ThenByDescending(worklog => worklog.CreatedAt)
            .ToArrayAsync(cancellationToken);

    public Task RemoveAsync(WorkItemWorklog worklog, CancellationToken cancellationToken)
    {
        dbContext.WorkItemWorklogs.Remove(worklog);
        return Task.CompletedTask;
    }
}
