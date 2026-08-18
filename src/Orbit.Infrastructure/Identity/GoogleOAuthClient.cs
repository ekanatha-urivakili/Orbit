using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Web;
using Microsoft.Extensions.Options;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;

namespace Orbit.Infrastructure.Identity;

internal sealed class GoogleOAuthClient(HttpClient httpClient, IOptions<GoogleOAuthOptions> options) : IGoogleOAuthClient
{
    private const string AuthorizeEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    public string BuildAuthorizeUrl(string state)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.RedirectUri))
        {
            throw new AuthenticationException("Sign in with Google is not configured for this installation.");
        }

        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = settings.ClientId;
        query["redirect_uri"] = settings.RedirectUri;
        query["response_type"] = "code";
        query["scope"] = "openid email profile";
        query["state"] = state;
        query["access_type"] = "online";
        query["prompt"] = "select_account";
        return $"{AuthorizeEndpoint}?{query}";
    }

    public async Task<string> ExchangeCodeForIdTokenAsync(string code, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.ClientSecret))
        {
            throw new AuthenticationException("Sign in with Google is not configured for this installation.");
        }

        var form = new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = settings.ClientId,
            ["client_secret"] = settings.ClientSecret,
            ["redirect_uri"] = settings.RedirectUri,
            ["grant_type"] = "authorization_code"
        };

        using var response = await httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new AuthenticationException("Google did not accept the sign-in request.");
        }

        var payload = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(payload?.IdToken))
        {
            throw new AuthenticationException("Google did not return an identity token.");
        }

        return payload.IdToken;
    }

    private sealed record GoogleTokenResponse([property: JsonPropertyName("id_token")] string? IdToken);
}
