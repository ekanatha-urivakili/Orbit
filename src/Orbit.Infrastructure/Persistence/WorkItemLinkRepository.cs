using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Choices;
using Orbit.Domain.WorkItems;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemLinkRepository(OrbitDbContext dbContext) : IWorkItemLinkRepository
{
    public async Task AddAsync(WorkItemLink link, CancellationToken cancellationToken) =>
        await dbContext.WorkItemLinks.AddAsync(link, cancellationToken);

    public Task<WorkItemLink?> GetAsync(Guid tenantId, Guid linkId, CancellationToken cancellationToken) =>
        dbContext.WorkItemLinks.SingleOrDefaultAsync(
            link => link.TenantId == tenantId && link.Id == linkId, cancellationToken);

    public async Task<IReadOnlyList<WorkItemLink>> ListByWorkItemAsync(
        Guid tenantId, Guid workItemId, CancellationToken cancellationToken) =>
        await dbContext.WorkItemLinks
            .Where(link => link.TenantId == tenantId
                && (link.SourceWorkItemId == workItemId || link.TargetWorkItemId == workItemId))
            .AsNoTracking()
            .OrderBy(link => link.CreatedAt)
            .ToArrayAsync(cancellationToken);

    public Task<bool> ExistsAsync(
        Guid tenantId,
        Guid sourceWorkItemId,
        Guid targetWorkItemId,
        WorkItemLinkKind kind,
        CancellationToken cancellationToken) =>
        dbContext.WorkItemLinks.AnyAsync(
            link => link.TenantId == tenantId
                && link.SourceWorkItemId == sourceWorkItemId
                && link.TargetWorkItemId == targetWorkItemId
                && link.Kind == kind,
            cancellationToken);

    public Task RemoveAsync(WorkItemLink link, CancellationToken cancellationToken)
    {
        dbContext.WorkItemLinks.Remove(link);
        return Task.CompletedTask;
    }
}
