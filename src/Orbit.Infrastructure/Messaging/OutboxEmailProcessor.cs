using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orbit.Infrastructure.Email;
using Orbit.Infrastructure.Persistence;

namespace Orbit.Infrastructure.Messaging;

/// <summary>
/// Claims a bounded batch of pending outbox emails and dispatches them. Uses <c>FOR UPDATE SKIP
/// LOCKED</c> inside one transaction rather than a time-based lease: a crashed process just drops
/// the open transaction and the row lock releases immediately for the next poll, which is simpler
/// and sufficient at the current single-worker scale (see ORBIT-WORK-MANAGEMENT-ARCHITECTURE.md
/// §13.3). Rows past <see cref="MaxAttempts"/> are left unpublished and excluded from future
/// claims - bounded retry without a dead-letter table, per §9.2 phase plan.
/// </summary>
public sealed class OutboxEmailProcessor(
    OrbitDbContext dbContext,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    ILogger<OutboxEmailProcessor> logger)
{
    private const int MaxAttempts = 5;
    private const int BatchSize = 20;

    public async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var claimed = await dbContext.OutboxEmailMessages
            .FromSqlInterpolated($"""
                SELECT * FROM outbox_email_messages
                WHERE published_at IS NULL AND attempts < {MaxAttempts}
                ORDER BY created_at
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        if (claimed.Count == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var message in claimed)
        {
            try
            {
                await emailSender.SendAsync(message.ToEmail, message.Subject, message.HtmlBody, cancellationToken);
                message.MarkPublished(now);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to send outbox email {MessageId}", message.Id);
                message.RecordFailure(exception.Message);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
