using System.Diagnostics;
using Orbit.Application.Abstractions;
using Orbit.Domain.Messaging;

namespace Orbit.Infrastructure.Persistence;

internal sealed class OutboxRepository(OrbitDbContext dbContext) : IOutboxRepository
{
    public async Task AddAsync(OutboxEmailMessage message, CancellationToken cancellationToken)
    {
        message.SetTraceParent(Activity.Current?.Id);
        await dbContext.OutboxEmailMessages.AddAsync(message, cancellationToken);
    }
}
