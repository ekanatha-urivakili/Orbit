using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orbit.Infrastructure.Email;
using Orbit.Infrastructure.Persistence;
using Orbit.Application.Access;
using Orbit.Domain.Access;

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
                var htmlBody = message.HtmlBody;
                WorkspaceInvitation? invitation = null;
                string? tokenHash = null;
                if (message.WorkspaceInvitationId is { } invitationId
                    && message.TenantId is { } tenantId
                    && message.FrontendBaseUrl is { } frontendBaseUrl)
                {
                    await dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT set_config('app.tenant_id', {tenantId.ToString()}, true)",
                        cancellationToken);
                    invitation = await dbContext.WorkspaceInvitations
                        .IgnoreQueryFilters()
                        .SingleOrDefaultAsync(
                            value => value.TenantId == tenantId && value.Id == invitationId,
                            cancellationToken);
                    if (invitation is null || invitation.Status != WorkspaceInvitationStatus.Active)
                    {
                        message.MarkPublished(now);
                        // Flush while app.tenant_id still matches this message's tenant - the
                        // batch loop moves on to switch it to the next message's tenant next.
                        await dbContext.SaveChangesAsync(cancellationToken);
                        continue;
                    }

                    var workspace = await dbContext.Workspaces
                        .AsNoTracking()
                        .SingleAsync(value => value.Id == tenantId, cancellationToken);
                    var rawToken = InvitationTokenCodec.Generate();
                    tokenHash = InvitationTokenCodec.Hash(rawToken);
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

                // Send before persisting the token rotation: if delivery fails, the invitation's
                // existing token (still valid) is untouched instead of being burned for nothing.
                await emailSender.SendAsync(message.ToEmail, message.Subject, htmlBody, cancellationToken);
                if (invitation is not null)
                {
                    invitation.Renew(
                        invitation.Role,
                        invitation.TeamId,
                        tokenHash!,
                        invitation.InvitedByMembershipId,
                        now,
                        TimeSpan.FromDays(7));
                }

                message.MarkPublished(now);
                // Flush per message, not once after the loop: app.tenant_id is transaction-local
                // and switches with every invitation message, so a single trailing SaveChangesAsync
                // would apply every prior message's invitation write under the *last* message's
                // tenant context and fail RLS for all but one tenant in a mixed-tenant batch.
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

        await transaction.CommitAsync(cancellationToken);
    }
}
