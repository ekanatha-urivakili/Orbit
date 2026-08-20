using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orbit.Application.Abstractions;
using Orbit.Domain.WorkItems;
using Orbit.Infrastructure.Persistence;

namespace Orbit.Infrastructure.Messaging;

/// <summary>
/// Claims a bounded batch of pending attachment-scan requests and runs them through
/// <see cref="IAttachmentScanner"/>, exactly mirroring <see cref="OutboxEmailProcessor"/>'s
/// <c>FOR UPDATE SKIP LOCKED</c>-via-transaction claim pattern and bounded-retry semantics
/// (§13.3/§9.2). Attachments and scan requests live in different tables so this cannot reuse that
/// processor directly, but the shape is the same.
/// </summary>
public sealed class AttachmentScanProcessor(
    OrbitDbContext dbContext,
    IAttachmentScanner scanner,
    IObjectStorageService storage,
    TimeProvider timeProvider,
    ILogger<AttachmentScanProcessor> logger)
{
    private const int MaxAttempts = 5;
    private const int BatchSize = 20;

    public async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        var pendingRequestIds = await dbContext.AttachmentScanRequests
            .AsNoTracking()
            .Where(request => request.ProcessedAt == null && request.Attempts < MaxAttempts)
            .OrderBy(request => request.CreatedAt)
            .Select(request => request.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pendingRequestIds.Count == 0)
        {
            return;
        }

        foreach (var requestId in pendingRequestIds)
        {
            try
            {
                await ProcessSingleRequestAsync(requestId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unexpected error processing attachment scan request {RequestId}", requestId);
            }
        }
    }

    private async Task ProcessSingleRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var request = await dbContext.AttachmentScanRequests
            .SingleOrDefaultAsync(
                r => r.Id == requestId && r.ProcessedAt == null && r.Attempts < MaxAttempts,
                cancellationToken);

        if (request is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        try
        {
            // The worker has no ambient tenant context (WorkerTenantContext.TenantId is always
            // Guid.Empty) - set app.tenant_id explicitly, same technique OutboxEmailProcessor uses
            // for the workspace-invitation row it touches, so the attachments table's FORCE RLS
            // policy admits this tenant's row.
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT set_config('app.tenant_id', {request.TenantId.ToString()}, true)",
                cancellationToken);

            var attachment = await dbContext.Attachments
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(
                    a => a.TenantId == request.TenantId && a.Id == request.AttachmentId,
                    cancellationToken);

            if (attachment is null || attachment.ScanStatus != AttachmentScanStatus.Pending)
            {
                // Attachment was deleted, or (defensively) already scanned - nothing left to do.
                request.MarkProcessed(now);
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            await using (var content = await storage.OpenReadAsync(request.ObjectKey, cancellationToken))
            {
                var result = await scanner.ScanAsync(content, attachment.FileName, cancellationToken);
                switch (result.Outcome)
                {
                    case AttachmentScanOutcome.Clean:
                        attachment.MarkScanned(AttachmentScanStatus.Clean, now);
                        break;
                    case AttachmentScanOutcome.Infected:
                        attachment.MarkScanned(AttachmentScanStatus.Infected, now);
                        logger.LogWarning(
                            "Attachment {AttachmentId} quarantined: {Detail}", attachment.Id, result.Detail);
                        await storage.MoveToQuarantineAsync(request.ObjectKey, cancellationToken);
                        break;
                    case AttachmentScanOutcome.Failed:
                    default:
                        attachment.MarkScanned(AttachmentScanStatus.Failed, now);
                        logger.LogWarning(
                            "Attachment {AttachmentId} scan failed: {Detail}", attachment.Id, result.Detail);
                        break;
                }
            }

            request.MarkProcessed(now);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to process attachment scan request {RequestId}", request.Id);
            request.RecordFailure(exception.Message);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
