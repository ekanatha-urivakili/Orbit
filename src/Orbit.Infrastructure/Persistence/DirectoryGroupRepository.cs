using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Directory;

namespace Orbit.Infrastructure.Persistence;

internal sealed class DirectoryGroupRepository(OrbitDbContext dbContext) : IDirectoryGroupRepository
{
    public async Task AddAsync(DirectoryGroup group, CancellationToken cancellationToken) =>
        await dbContext.DirectoryGroups.AddAsync(group, cancellationToken);

    public Task<DirectoryGroup?> GetAsync(Guid tenantId, Guid groupId, CancellationToken cancellationToken) =>
        dbContext.DirectoryGroups.SingleOrDefaultAsync(
            group => group.TenantId == tenantId && group.Id == groupId,
            cancellationToken);

    public async Task<IReadOnlyList<DirectoryGroup>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await dbContext.DirectoryGroups
            .AsNoTracking()
            .Where(group => group.TenantId == tenantId)
            .OrderBy(group => group.Name)
            .ToArrayAsync(cancellationToken);
}

internal sealed class GroupMembershipRepository(OrbitDbContext dbContext) : IGroupMembershipRepository
{
    public async Task AddAsync(GroupMembership membership, CancellationToken cancellationToken) =>
        await dbContext.GroupMemberships.AddAsync(membership, cancellationToken);

    public Task RemoveAsync(GroupMembership membership, CancellationToken cancellationToken)
    {
        dbContext.GroupMemberships.Remove(membership);
        return Task.CompletedTask;
    }

    public Task<GroupMembership?> GetAsync(
        Guid tenantId,
        Guid groupId,
        Guid membershipId,
        CancellationToken cancellationToken) =>
        dbContext.GroupMemberships.SingleOrDefaultAsync(
            membership => membership.TenantId == tenantId
                && membership.GroupId == groupId
                && membership.MembershipId == membershipId,
            cancellationToken);

    public async Task<IReadOnlyList<GroupMembership>> ListByGroupAsync(
        Guid tenantId,
        Guid groupId,
        CancellationToken cancellationToken) =>
        await dbContext.GroupMemberships
            .AsNoTracking()
            .Where(membership => membership.TenantId == tenantId && membership.GroupId == groupId)
            .OrderBy(membership => membership.CreatedAt)
            .ToArrayAsync(cancellationToken);
}
