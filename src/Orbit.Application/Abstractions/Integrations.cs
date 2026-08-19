using Orbit.Domain.Integrations;

namespace Orbit.Application.Abstractions;

public sealed record SlackIncomingWebhook(
    string AccessToken, string TeamId, string TeamName, string ChannelId, string ChannelName, string WebhookUrl);

/// <summary>
/// Thin wrapper over Slack's OAuth + Incoming Webhook HTTP API. Kept out of Application so command
/// handlers stay free of HTTP/SDK details, matching the <c>IEmailSender</c>/<c>IGoogleOAuthClient</c>
/// abstraction shape already used for other third-party integrations.
/// </summary>
public interface ISlackClient
{
    string BuildAuthorizeUrl(string state);

    Task<SlackIncomingWebhook> ExchangeCodeAsync(string code, CancellationToken cancellationToken);

    Task PostMessageAsync(string webhookUrl, string text, CancellationToken cancellationToken);
}

public interface ISlackConnectionRepository
{
    Task AddAsync(SlackConnection connection, CancellationToken cancellationToken);

    Task<SlackConnection?> GetByProjectAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken);

    Task<SlackConnection?> GetAsync(Guid tenantId, Guid connectionId, CancellationToken cancellationToken);

    Task RemoveAsync(SlackConnection connection, CancellationToken cancellationToken);
}
