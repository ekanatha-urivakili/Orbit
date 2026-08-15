using Orbit.Application.Abstractions;
using Orbit.Domain.Messaging;

namespace Orbit.Infrastructure.Persistence;

internal sealed class OutboxRepository(OrbitDbContext dbContext) : IOutboxRepository
{
    public async Task AddAsync(OutboxEmailMessage message, CancellationToken cancellationToken) =>
        await dbContext.OutboxEmailMessages.AddAsync(message, cancellationToken);
}
