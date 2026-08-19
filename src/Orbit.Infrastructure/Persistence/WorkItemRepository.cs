using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.WorkItems;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemRepository(
    OrbitDbContext dbContext,
    ICurrentPrincipal principal) : IWorkItemRepository
{
    public async Task AddAsync(WorkItem workItem, CancellationToken cancellationToken) =>
        await dbContext.WorkItems.AddAsync(workItem, cancellationToken);

    public Task<WorkItem?> GetAsync(
        Guid tenantId,
        Guid workItemId,
        ProjectPermission permission,
        CancellationToken cancellationToken) =>
        PermittedWorkItems(tenantId, permission).SingleOrDefaultAsync(
            workItem => workItem.Id == workItemId,
            cancellationToken);

    public async Task<PagedResult<WorkItem>> ListByProjectAsync(
        Guid tenantId,
        Guid projectId,
        ProjectPermission permission,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = PermittedWorkItems(tenantId, permission)
            .AsNoTracking()
            .Where(workItem => workItem.ProjectId == projectId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(workItem => workItem.Rank)
            .ThenBy(workItem => workItem.Key)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken);
        return new PagedResult<WorkItem>(items, totalCount);
    }

    public async Task<IReadOnlyList<WorkItem>> ListByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> workItemIds,
        ProjectPermission permission,
        CancellationToken cancellationToken) =>
        await PermittedWorkItems(tenantId, permission)
            .Where(workItem => workItemIds.Contains(workItem.Id))
            .ToArrayAsync(cancellationToken);

    public Task<bool> HasChildrenAsync(
        Guid tenantId, Guid parentWorkItemId, CancellationToken cancellationToken) =>
        dbContext.WorkItems
            .AsNoTracking()
            .AnyAsync(
                workItem => workItem.TenantId == tenantId && workItem.ParentId == parentWorkItemId,
                cancellationToken);

    public Task RemoveAsync(WorkItem workItem, CancellationToken cancellationToken)
    {
        dbContext.WorkItems.Remove(workItem);
        return Task.CompletedTask;
    }

    private IQueryable<WorkItem> PermittedWorkItems(Guid tenantId, ProjectPermission permission)
    {
        var permittedProjectIds = ProjectAccessQuery
            .PermittedProjects(dbContext, principal, tenantId, permission)
            .Select(project => project.Id);

        return dbContext.WorkItems.Where(workItem =>
            workItem.TenantId == tenantId
            && permittedProjectIds.Contains(workItem.ProjectId));
    }
}
