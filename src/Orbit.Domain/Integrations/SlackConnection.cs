using Orbit.Domain.Common;

namespace Orbit.Domain.Integrations;

/// <summary>
/// A tenant project's connection to a single Slack channel via an Incoming Webhook. One connection
/// per project; reconnecting replaces the previous webhook. The webhook URL itself is a bearer
/// credential (anyone holding it can post to the channel), so it's only ever stored encrypted
/// (<see cref="EncryptedWebhookUrl"/>) via the caller's secret-protection service.
/// </summary>
public sealed class SlackConnection
{
    private SlackConnection()
    {
    }

    private SlackConnection(
        Guid id,
        Guid tenantId,
        Guid projectId,
        string teamId,
        string teamName,
        string channelId,
        string channelName,
        string encryptedWebhookUrl,
        Guid connectedByUserId,
        DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        TeamId = teamId;
        TeamName = teamName;
        ChannelId = channelId;
        ChannelName = channelName;
        EncryptedWebhookUrl = encryptedWebhookUrl;
        ConnectedByUserId = connectedByUserId;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string TeamId { get; private set; } = string.Empty;
    public string TeamName { get; private set; } = string.Empty;
    public string ChannelId { get; private set; } = string.Empty;
    public string ChannelName { get; private set; } = string.Empty;
    public string EncryptedWebhookUrl { get; private set; } = string.Empty;
    public Guid ConnectedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static SlackConnection Create(
        Guid tenantId,
        Guid projectId,
        string teamId,
        string teamName,
        string channelId,
        string channelName,
        string encryptedWebhookUrl,
        Guid connectedByUserId,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty || connectedByUserId == Guid.Empty)
        {
            throw new DomainException("Tenant, project, and connecting user ids are required.");
        }

        if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(channelId)
            || string.IsNullOrWhiteSpace(encryptedWebhookUrl))
        {
            throw new DomainException("Slack team, channel, and webhook are required.");
        }

        return new SlackConnection(
            Guid.CreateVersion7(), tenantId, projectId, teamId, teamName, channelId, channelName,
            encryptedWebhookUrl, connectedByUserId, now);
    }
}
