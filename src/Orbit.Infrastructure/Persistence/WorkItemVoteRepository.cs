using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.WorkItems;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemVoteRepository(OrbitDbContext dbContext) : IWorkItemVoteRepository
{
    // Raw insert so concurrent duplicate vote requests are idempotent instead of one losing to
    // ux_work_item_votes_tenant_item_user with an unhandled unique-constraint violation.
    public async Task AddAsync(WorkItemVote vote, CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO work_item_votes (id, tenant_id, work_item_id, user_id, created_at)
            VALUES ({vote.Id}, {vote.TenantId}, {vote.WorkItemId}, {vote.UserId}, {vote.CreatedAt})
            ON CONFLICT (tenant_id, work_item_id, user_id) DO NOTHING
            """,
            cancellationToken);

    public Task<WorkItemVote?> GetAsync(
        Guid tenantId, Guid workItemId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.WorkItemVotes
            .SingleOrDefaultAsync(
                vote => vote.TenantId == tenantId && vote.WorkItemId == workItemId && vote.UserId == userId,
                cancellationToken);

    public async Task<IReadOnlyList<WorkItemVote>> ListByWorkItemAsync(
        Guid tenantId, Guid workItemId, CancellationToken cancellationToken) =>
        await dbContext.WorkItemVotes
            .AsNoTracking()
            .Where(vote => vote.TenantId == tenantId && vote.WorkItemId == workItemId)
            .ToArrayAsync(cancellationToken);

    public Task RemoveAsync(WorkItemVote vote, CancellationToken cancellationToken)
    {
        dbContext.WorkItemVotes.Remove(vote);
        return Task.CompletedTask;
    }
}
