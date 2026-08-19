namespace Orbit.Infrastructure.Identity;

/// <summary>
/// Bound from configuration section <c>Authentication:Google</c>. Unset (empty <see cref="ClientId"/>)
/// simply means "Sign in with Google" is unavailable - <see cref="GoogleOAuthClient"/> callers should
/// treat that as a disabled feature, not a startup failure, so installations that don't want Google
/// sign-in need no configuration at all.
/// </summary>
public sealed class GoogleOAuthOptions
{
    public const string SectionName = "Authentication:Google";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Must exactly match a redirect URI registered on the Google OAuth client - this backend's own callback endpoint.</summary>
    public string RedirectUri { get; set; } = string.Empty;
}
