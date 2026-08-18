using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Access;

namespace Orbit.Infrastructure.Persistence;

internal sealed class TenantMembershipRepository(OrbitDbContext dbContext) : ITenantMembershipRepository
{
    public async Task AddAsync(TenantMembership membership, CancellationToken cancellationToken) =>
        await dbContext.TenantMemberships.AddAsync(membership, cancellationToken);

    public Task<TenantMembership?> GetActiveAsync(
        Guid tenantId,
        string issuer,
        string subject,
        CancellationToken cancellationToken) =>
        dbContext.TenantMemberships
            .AsNoTracking()
            .SingleOrDefaultAsync(
                membership => membership.TenantId == tenantId
                    && membership.Issuer == issuer
                    && membership.Subject == subject
                    && membership.IsActive,
                cancellationToken);

    public Task<TenantMembership?> GetActiveByUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.TenantMemberships.SingleOrDefaultAsync(
            membership => membership.TenantId == tenantId
                && membership.UserId == userId
                && membership.IsActive,
            cancellationToken);

    public Task<TenantMembership?> GetActiveAsync(
        Guid tenantId,
        Guid membershipId,
        CancellationToken cancellationToken) =>
        dbContext.TenantMemberships.SingleOrDefaultAsync(
            membership => membership.TenantId == tenantId
                && membership.Id == membershipId
                && membership.IsActive,
            cancellationToken);

    public Task<TenantMembership?> GetOwnerAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.TenantMemberships
            .AsNoTracking()
            .OrderBy(membership => membership.CreatedAt)
            .FirstOrDefaultAsync(
                membership => membership.TenantId == tenantId
                    && membership.UserId != null
                    && membership.Role == TenantRole.Owner
                    && membership.IsActive,
                cancellationToken);

    public async Task<IReadOnlyList<TenantMembership>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await dbContext.TenantMemberships
            .AsNoTracking()
            .Where(membership => membership.TenantId == tenantId)
            .OrderBy(membership => membership.CreatedAt)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<TenantMembership>> ListByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> membershipIds,
        CancellationToken cancellationToken)
    {
        if (membershipIds.Count == 0) return [];
        return await dbContext.TenantMemberships
            .AsNoTracking()
            .Where(membership => membership.TenantId == tenantId && membershipIds.Contains(membership.Id))
            .ToArrayAsync(cancellationToken);
    }
}
