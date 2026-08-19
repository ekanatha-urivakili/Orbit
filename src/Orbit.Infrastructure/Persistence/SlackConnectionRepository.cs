using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Integrations;

namespace Orbit.Infrastructure.Persistence;

internal sealed class SlackConnectionRepository(OrbitDbContext dbContext) : ISlackConnectionRepository
{
    public async Task AddAsync(SlackConnection connection, CancellationToken cancellationToken) =>
        await dbContext.SlackConnections.AddAsync(connection, cancellationToken);

    public Task<SlackConnection?> GetByProjectAsync(
        Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.SlackConnections
            .SingleOrDefaultAsync(
                connection => connection.TenantId == tenantId && connection.ProjectId == projectId,
                cancellationToken);

    public Task<SlackConnection?> GetAsync(Guid tenantId, Guid connectionId, CancellationToken cancellationToken) =>
        dbContext.SlackConnections
            .SingleOrDefaultAsync(
                connection => connection.TenantId == tenantId && connection.Id == connectionId, cancellationToken);

    public Task RemoveAsync(SlackConnection connection, CancellationToken cancellationToken)
    {
        dbContext.SlackConnections.Remove(connection);
        return Task.CompletedTask;
    }
}
