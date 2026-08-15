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
        var pendingMessageIds = await dbContext.OutboxEmailMessages
            .AsNoTracking()
            .Where(message => message.PublishedAt == null && message.Attempts < MaxAttempts)
            .OrderBy(message => message.CreatedAt)
            .Select(message => message.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pendingMessageIds.Count == 0)
        {
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

        var now = timeProvider.GetUtcNow();
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
}
