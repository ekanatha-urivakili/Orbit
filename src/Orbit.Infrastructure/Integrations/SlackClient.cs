using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Web;
using FluentValidation;
using Microsoft.Extensions.Options;
using Orbit.Application.Abstractions;

namespace Orbit.Infrastructure.Integrations;

internal sealed class SlackClient(HttpClient httpClient, IOptions<SlackOptions> options) : ISlackClient
{
    private const string AuthorizeEndpoint = "https://slack.com/oauth/v2/authorize";
    private const string TokenEndpoint = "https://slack.com/api/oauth.v2.access";

    public string BuildAuthorizeUrl(string state)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.RedirectUri))
        {
            throw new ValidationException("Slack is not configured for this installation.");
        }

        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = settings.ClientId;
        query["redirect_uri"] = settings.RedirectUri;
        // The `incoming-webhook` scope lets the connecting user pick a channel on Slack's consent
        // screen; the token exchange then returns a webhook URL scoped to just that channel, so
        // Orbit never needs a general bot token or a separate channel-picker API call.
        query["scope"] = "incoming-webhook";
        query["state"] = state;
        return $"{AuthorizeEndpoint}?{query}";
    }

    public async Task<SlackIncomingWebhook> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.ClientSecret))
        {
            throw new ValidationException("Slack is not configured for this installation.");
        }

        var form = new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = settings.ClientId,
            ["client_secret"] = settings.ClientSecret,
            ["redirect_uri"] = settings.RedirectUri,
        };

        using var response = await httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form), cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<SlackOAuthResponse>(cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode || payload is null || !payload.Ok || payload.IncomingWebhook is null)
        {
            throw new ValidationException(
                $"Slack did not accept the connection request{(payload?.Error is { } error ? $": {error}" : ".")}");
        }

        return new SlackIncomingWebhook(
            AccessToken: payload.AccessToken ?? string.Empty,
            TeamId: payload.Team?.Id ?? string.Empty,
            TeamName: payload.Team?.Name ?? string.Empty,
            ChannelId: payload.IncomingWebhook.ChannelId ?? string.Empty,
            ChannelName: payload.IncomingWebhook.Channel ?? string.Empty,
            WebhookUrl: payload.IncomingWebhook.Url ?? string.Empty);
    }

    public async Task PostMessageAsync(string webhookUrl, string text, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(webhookUrl, new { text }, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ValidationException("Slack did not accept the message.");
        }
    }

    private sealed record SlackOAuthResponse(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("team")] SlackTeam? Team,
        [property: JsonPropertyName("incoming_webhook")] SlackIncomingWebhookPayload? IncomingWebhook);

    private sealed record SlackTeam(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name);

    private sealed record SlackIncomingWebhookPayload(
        [property: JsonPropertyName("channel")] string? Channel,
        [property: JsonPropertyName("channel_id")] string? ChannelId,
        [property: JsonPropertyName("url")] string? Url);
}
