using Orbit.Application.Abstractions;
using Orbit.Domain.Messaging;

namespace Orbit.Infrastructure.Persistence;

internal sealed class AttachmentScanRequestRepository(OrbitDbContext dbContext) : IAttachmentScanRequestRepository
{
    public async Task AddAsync(AttachmentScanRequest request, CancellationToken cancellationToken) =>
        await dbContext.AttachmentScanRequests.AddAsync(request, cancellationToken);
}
