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
        Guid projectId,
        Guid id,
        CancellationToken cancellationToken) =>
        dbContext.CustomFieldDefinitions.SingleOrDefaultAsync(
            definition => definition.TenantId == tenantId && definition.ProjectId == projectId && definition.Id == id,
            cancellationToken);

    public Task<CustomFieldDefinition?> GetByKeyAsync(
        Guid tenantId,
        Guid projectId,
        string key,
        CancellationToken cancellationToken) =>
        dbContext.CustomFieldDefinitions.AsNoTracking().SingleOrDefaultAsync(
            definition => definition.TenantId == tenantId && definition.ProjectId == projectId && definition.Key == key,
            cancellationToken);

    public async Task<IReadOnlyList<CustomFieldDefinition>> ListAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken) =>
        await dbContext.CustomFieldDefinitions
            .AsNoTracking()
            .Where(definition => definition.TenantId == tenantId && definition.ProjectId == projectId)
            .OrderBy(definition => definition.Order)
            .ThenBy(definition => definition.Label)
            .ToArrayAsync(cancellationToken);
}
