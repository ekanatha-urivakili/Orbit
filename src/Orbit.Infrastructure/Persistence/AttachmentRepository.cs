using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.WorkItems;

namespace Orbit.Infrastructure.Persistence;

internal sealed class AttachmentRepository(OrbitDbContext dbContext) : IAttachmentRepository
{
    public async Task AddAsync(Attachment attachment, CancellationToken cancellationToken) =>
        await dbContext.Attachments.AddAsync(attachment, cancellationToken);

    public Task<Attachment?> GetAsync(
        Guid tenantId,
        Guid workItemId,
        Guid attachmentId,
        CancellationToken cancellationToken) =>
        dbContext.Attachments
            .SingleOrDefaultAsync(
                attachment =>
                    attachment.TenantId == tenantId
                    && attachment.WorkItemId == workItemId
                    && attachment.Id == attachmentId,
                cancellationToken);

    public async Task<IReadOnlyList<Attachment>> ListByWorkItemAsync(
        Guid tenantId,
        Guid workItemId,
        CancellationToken cancellationToken) =>
        await dbContext.Attachments
            .AsNoTracking()
            .Where(attachment => attachment.TenantId == tenantId && attachment.WorkItemId == workItemId)
            .OrderBy(attachment => attachment.UploadedAt)
            .ThenBy(attachment => attachment.Id)
            .ToArrayAsync(cancellationToken);

    public Task RemoveAsync(Attachment attachment, CancellationToken cancellationToken)
    {
        dbContext.Attachments.Remove(attachment);
        return Task.CompletedTask;
    }
}
