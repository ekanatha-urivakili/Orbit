using System.Security.Claims;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;

namespace Orbit.Infrastructure.Identity;

internal sealed class GoogleIdTokenValidator : IGoogleIdTokenValidator
{
    private const string GoogleAuthority = "https://accounts.google.com";

    private readonly IOptions<GoogleOAuthOptions> _options;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
    private readonly JsonWebTokenHandler _handler = new();

    public GoogleIdTokenValidator(IOptions<GoogleOAuthOptions> options, IHostEnvironment environment)
    {
        _options = options;
        _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{GoogleAuthority}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = !environment.IsDevelopment() });
    }

    public async Task<VerifiedGoogleIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        var clientId = _options.Value.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new AuthenticationException("Sign in with Google is not configured for this installation.");
        }

        try
        {
            var metadata = await _configurationManager.GetConfigurationAsync(cancellationToken);
            var result = await _handler.ValidateTokenAsync(idToken, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuers = [metadata.Issuer, GoogleAuthority, "accounts.google.com"],
                ValidateAudience = true,
                ValidAudience = clientId,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = metadata.SigningKeys,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            });

            if (!result.IsValid || result.ClaimsIdentity is null)
            {
                throw new AuthenticationException("Google's identity proof is invalid or expired.");
            }

            var subject = result.ClaimsIdentity.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? result.ClaimsIdentity.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(subject))
            {
                throw new AuthenticationException("Google's identity proof has no subject.");
            }

            var email = result.ClaimsIdentity.FindFirst(ClaimTypes.Email)?.Value
                ?? result.ClaimsIdentity.FindFirst("email")?.Value;
            var emailVerified = bool.TryParse(
                result.ClaimsIdentity.FindFirst("email_verified")?.Value,
                out var parsedEmailVerified) && parsedEmailVerified;
            var name = result.ClaimsIdentity.FindFirst("name")?.Value;

            return new VerifiedGoogleIdentity(subject, email, emailVerified, name);
        }
        catch (AuthenticationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is SecurityTokenException or InvalidOperationException)
        {
            throw new AuthenticationException("Google's identity proof is invalid or expired.");
        }
    }
}
