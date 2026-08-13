using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Boards;

namespace Orbit.Infrastructure.Persistence;

internal sealed class SprintMembershipRepository(OrbitDbContext dbContext) : ISprintMembershipRepository
{
    public async Task AddAsync(SprintMembership membership, CancellationToken cancellationToken) =>
        await dbContext.SprintMemberships.AddAsync(membership, cancellationToken);

    public Task<SprintMembership?> GetCurrentByWorkItemAsync(
        Guid tenantId,
        Guid workItemId,
        CancellationToken cancellationToken) =>
        dbContext.SprintMemberships.SingleOrDefaultAsync(
            membership => membership.TenantId == tenantId
                && membership.WorkItemId == workItemId
                && membership.RemovedAt == null,
            cancellationToken);

    public async Task<IReadOnlyList<SprintMembership>> ListCurrentBySprintAsync(
        Guid tenantId,
        Guid sprintId,
        CancellationToken cancellationToken) =>
        await dbContext.SprintMemberships
            .Where(membership => membership.TenantId == tenantId
                && membership.SprintId == sprintId
                && membership.RemovedAt == null)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SprintMembership>> ListCurrentBySprintsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> sprintIds,
        CancellationToken cancellationToken) =>
        await dbContext.SprintMemberships
            .AsNoTracking()
            .Where(membership => membership.TenantId == tenantId
                && sprintIds.Contains(membership.SprintId)
                && membership.RemovedAt == null)
            .ToArrayAsync(cancellationToken);
}
