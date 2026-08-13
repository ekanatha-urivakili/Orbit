using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;

namespace Orbit.Infrastructure.Identity;

internal sealed class ExternalIdentityTokenValidator : IExternalIdentityTokenValidator
{
    private readonly string? _authority;
    private readonly string? _audience;
    private readonly ConfigurationManager<OpenIdConnectConfiguration>? _configurationManager;
    private readonly JsonWebTokenHandler _handler = new();

    public ExternalIdentityTokenValidator(IConfiguration configuration)
    {
        _authority = configuration["Authentication:Authority"]?.TrimEnd('/');
        _audience = configuration["Authentication:ExternalIdentityAudience"];
        if (!string.IsNullOrWhiteSpace(_authority))
        {
            _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{_authority}/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever());
        }
    }

    public async Task<VerifiedExternalIdentity> ValidateAsync(
        string token,
        CancellationToken cancellationToken)
    {
        if (_configurationManager is null || string.IsNullOrWhiteSpace(_audience))
        {
            throw new AuthenticationException(
                "External identity linking is not configured for this installation.");
        }

        try
        {
            var metadata = await _configurationManager.GetConfigurationAsync(cancellationToken);
            var result = await _handler.ValidateTokenAsync(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = metadata.Issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = metadata.SigningKeys,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            });

            if (!result.IsValid || result.ClaimsIdentity is null)
            {
                throw new AuthenticationException("The external identity proof is invalid or expired.");
            }

            var issuer = result.ClaimsIdentity.FindFirst("iss")?.Value;
            var subject = result.ClaimsIdentity.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? result.ClaimsIdentity.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject))
            {
                throw new AuthenticationException("The external identity proof has no issuer or subject.");
            }

            return new VerifiedExternalIdentity(issuer, subject);
        }
        catch (AuthenticationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is SecurityTokenException or InvalidOperationException)
        {
            throw new AuthenticationException("The external identity proof is invalid or expired.");
        }
    }
}
