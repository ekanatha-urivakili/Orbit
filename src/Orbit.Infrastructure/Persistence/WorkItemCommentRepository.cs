using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.WorkItems;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemCommentRepository(OrbitDbContext dbContext) : IWorkItemCommentRepository
{
    public async Task AddAsync(WorkItemComment comment, CancellationToken cancellationToken) =>
        await dbContext.WorkItemComments.AddAsync(comment, cancellationToken);

    public Task<WorkItemComment?> GetAsync(
        Guid tenantId,
        Guid workItemId,
        Guid commentId,
        CancellationToken cancellationToken) =>
        dbContext.WorkItemComments
            .SingleOrDefaultAsync(
                comment =>
                    comment.TenantId == tenantId
                    && comment.WorkItemId == workItemId
                    && comment.Id == commentId,
                cancellationToken);

    public async Task<IReadOnlyList<WorkItemComment>> ListByWorkItemAsync(
        Guid tenantId,
        Guid workItemId,
        CancellationToken cancellationToken) =>
        await dbContext.WorkItemComments
            .AsNoTracking()
            .Where(comment => comment.TenantId == tenantId && comment.WorkItemId == workItemId)
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .ToArrayAsync(cancellationToken);
}
