using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.WorkItems;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemCustomFieldValueRepository(OrbitDbContext dbContext) : IWorkItemCustomFieldValueRepository
{
    public Task<WorkItemCustomFieldValue?> GetAsync(
        Guid tenantId, Guid workItemId, Guid customFieldDefinitionId, CancellationToken cancellationToken) =>
        dbContext.WorkItemCustomFieldValues.SingleOrDefaultAsync(
            value => value.TenantId == tenantId
                && value.WorkItemId == workItemId
                && value.CustomFieldDefinitionId == customFieldDefinitionId,
            cancellationToken);

    public async Task<IReadOnlyList<WorkItemCustomFieldValue>> ListByWorkItemAsync(
        Guid tenantId, Guid workItemId, CancellationToken cancellationToken) =>
        await dbContext.WorkItemCustomFieldValues
            .Where(value => value.TenantId == tenantId && value.WorkItemId == workItemId)
            .ToArrayAsync(cancellationToken);

    public async Task AddAsync(WorkItemCustomFieldValue value, CancellationToken cancellationToken) =>
        await dbContext.WorkItemCustomFieldValues.AddAsync(value, cancellationToken);

    public Task RemoveAsync(WorkItemCustomFieldValue value, CancellationToken cancellationToken)
    {
        dbContext.WorkItemCustomFieldValues.Remove(value);
        return Task.CompletedTask;
    }
}
