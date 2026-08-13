namespace Orbit.Infrastructure.Identity;

/// <summary>
/// Bound from configuration section <c>Authentication:Local</c>. <see cref="SigningKey"/> must be
/// a base64-encoded key of at least 32 bytes; it is required to issue or validate locally-signed
/// access tokens and has no safe default in a shipped configuration file.
/// </summary>
public sealed class LocalTokenOptions
{
    public const string SectionName = "Authentication:Local";
    public const string DefaultIssuer = "urn:orbit:local";
    public const string DefaultAudience = "orbit-api";

    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = DefaultIssuer;
    public string Audience { get; set; } = DefaultAudience;
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
    public int RefreshTokenLifetimeDays { get; set; } = 30;
}
