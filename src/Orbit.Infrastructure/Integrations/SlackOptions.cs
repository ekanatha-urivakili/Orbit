namespace Orbit.Infrastructure.Integrations;

public sealed class SlackOptions
{
    public const string SectionName = "Slack";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SigningSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
}
