using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Choices;
using Orbit.Domain.Configuration;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemTypeRepository(OrbitDbContext dbContext) : IWorkItemTypeRepository
{
    public Task<WorkItemTypeDefinition?> GetAsync(
        Guid tenantId,
        WorkItemType id,
        CancellationToken cancellationToken) =>
        dbContext.WorkItemTypeDefinitions.SingleOrDefaultAsync(
            definition => definition.TenantId == tenantId && definition.Id == id,
            cancellationToken);

    public async Task<IReadOnlyList<WorkItemTypeDefinition>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await dbContext.WorkItemTypeDefinitions
            .Where(definition => definition.TenantId == tenantId)
            .OrderBy(definition => definition.Order)
            .ThenBy(definition => definition.Label)
            .ToArrayAsync(cancellationToken);
}
