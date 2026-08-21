using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orbit.Infrastructure.Email;
using Orbit.Infrastructure.Persistence;
using Orbit.Application.Access;
using Orbit.Domain.Access;

namespace Orbit.Infrastructure.Messaging;

/// <summary>
/// Claims a bounded batch of pending outbox emails and dispatches them. The claim query itself
/// runs <c>FOR UPDATE SKIP LOCKED</c> inside one transaction held open for the whole batch, so a
/// second worker replica (or an overlapping poll during a rolling deploy) skips any row this
/// instance already holds rather than double-sending it. A crashed process just drops the open
/// transaction and the row lock releases immediately for the next poll - a time-based lease was
/// judged unnecessary while the worker runs at single-replica scale (see
/// ORBIT-WORK-MANAGEMENT-ARCHITECTURE.md §13.3). Rows past <see cref="MaxAttempts"/> are left
/// unpublished and excluded from future claims - bounded retry without a dead-letter table, per
/// §9.2 phase plan.
/// </summary>
public sealed class OutboxEmailProcessor(
    OrbitDbContext dbContext,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    ILogger<OutboxEmailProcessor> logger)
{
    private const int MaxAttempts = 5;
    private const int BatchSize = 20;

    public const string ActivitySourceName = "Orbit.Worker.Outbox";
    public const string MeterName = "Orbit.Worker.Outbox";

    // §13.7.2 (ADR-023): re-parents the worker's span under the trace captured at insert time
    // (OutboxRepository.AddAsync), so "comment posted" and "mention email sent" join into one
    // trace across the API/worker process boundary instead of two orphaned spans.
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName);
    private static readonly Histogram<double> OutboxLagSeconds =
        Meter.CreateHistogram<double>("outbox_lag_seconds", unit: "s");

    public async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        var pendingMessageIds = await dbContext.Database
            .SqlQuery<Guid>($"""
                SELECT id FROM outbox_email_messages
                WHERE published_at IS NULL AND attempts < {MaxAttempts}
                    AND (not_before IS NULL OR not_before <= {now})
                ORDER BY created_at
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        if (pendingMessageIds.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        foreach (var messageId in pendingMessageIds)
        {
            try
            {
                await ProcessSingleMessageAsync(messageId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unexpected error processing outbox email {MessageId}", messageId);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ProcessSingleMessageAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var message = await dbContext.OutboxEmailMessages
            .SingleOrDefaultAsync(
                m => m.Id == messageId && m.PublishedAt == null && m.Attempts < MaxAttempts,
                cancellationToken);

        if (message is null)
        {
            return;
        }

        using var activity = StartDispatchActivity(message.TraceParent);
        var now = timeProvider.GetUtcNow();
        OutboxLagSeconds.Record((now - message.CreatedAt).TotalSeconds);
        try
        {
            var htmlBody = message.HtmlBody;
            if (message.WorkspaceInvitationId is { } invitationId
                && message.TenantId is { } tenantId
                && message.FrontendBaseUrl is { } frontendBaseUrl)
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT set_config('app.tenant_id', {tenantId.ToString()}, true)",
                    cancellationToken);

                var invitation = await dbContext.WorkspaceInvitations
                    .IgnoreQueryFilters()
                    .SingleOrDefaultAsync(
                        value => value.TenantId == tenantId && value.Id == invitationId,
                        cancellationToken);

                if (invitation is null || invitation.Status != WorkspaceInvitationStatus.Active)
                {
                    message.MarkPublished(now);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return;
                }

                var workspace = await dbContext.Workspaces
                    .AsNoTracking()
                    .SingleAsync(value => value.Id == tenantId, cancellationToken);

                var rawToken = InvitationTokenCodec.Generate();
                var tokenHash = InvitationTokenCodec.Hash(rawToken);

                invitation.Renew(
                    invitation.Role,
                    invitation.TeamId,
                    tokenHash,
                    invitation.InvitedByMembershipId,
                    now,
                    TimeSpan.FromDays(7));

                await dbContext.SaveChangesAsync(cancellationToken);

                var linkBuilder = new UriBuilder(frontendBaseUrl)
                {
                    Fragment = $"invitationToken={Uri.EscapeDataString(rawToken)}" +
                        $"&invitationTenantId={tenantId:D}"
                };
                var link = System.Net.WebUtility.HtmlEncode(linkBuilder.Uri.AbsoluteUri);
                htmlBody = $"""
                    <p>You have been invited to join {System.Net.WebUtility.HtmlEncode(workspace.Name)} on Orbit.</p>
                    <p><a href="{link}">Accept invitation</a></p>
                    <p>This invitation expires in seven days and can only be used once.</p>
                    """;
            }

            await emailSender.SendAsync(message.ToEmail, message.Subject, htmlBody, cancellationToken);

            message.MarkPublished(now);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to send outbox email {MessageId}", message.Id);
            message.RecordFailure(exception.Message);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static Activity? StartDispatchActivity(string? traceParent)
    {
        if (traceParent is not null && ActivityContext.TryParse(traceParent, null, out var parentContext))
        {
            return ActivitySource.StartActivity("outbox.email.dispatch", ActivityKind.Consumer, parentContext);
        }

        return ActivitySource.StartActivity("outbox.email.dispatch", ActivityKind.Consumer);
    }
}
