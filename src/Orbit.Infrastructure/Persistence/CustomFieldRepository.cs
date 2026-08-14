using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Configuration;

namespace Orbit.Infrastructure.Persistence;

internal sealed class CustomFieldRepository(OrbitDbContext dbContext) : ICustomFieldRepository
{
    public async Task AddAsync(CustomFieldDefinition definition, CancellationToken cancellationToken) =>
        await dbContext.CustomFieldDefinitions.AddAsync(definition, cancellationToken);

    public Task<CustomFieldDefinition?> GetAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken) =>
        dbContext.CustomFieldDefinitions.SingleOrDefaultAsync(
            definition => definition.TenantId == tenantId && definition.Id == id,
            cancellationToken);

    public Task<CustomFieldDefinition?> GetByKeyAsync(
        Guid tenantId,
        string key,
        CancellationToken cancellationToken) =>
        dbContext.CustomFieldDefinitions.SingleOrDefaultAsync(
            definition => definition.TenantId == tenantId && definition.Key == key,
            cancellationToken);

    public async Task<IReadOnlyList<CustomFieldDefinition>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await dbContext.CustomFieldDefinitions
            .Where(definition => definition.TenantId == tenantId)
            .OrderBy(definition => definition.Order)
            .ThenBy(definition => definition.Label)
            .ToArrayAsync(cancellationToken);
}
