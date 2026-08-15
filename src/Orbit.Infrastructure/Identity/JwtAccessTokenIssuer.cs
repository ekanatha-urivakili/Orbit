using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Orbit.Application.Abstractions;

namespace Orbit.Infrastructure.Identity;

internal sealed class JwtAccessTokenIssuer(IOptions<LocalTokenOptions> options) : IAccessTokenIssuer
{
    private readonly JwtSecurityTokenHandler _handler = new();

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(Math.Max(1, options.Value.RefreshTokenLifetimeDays));

    public string LocalIssuer => options.Value.Issuer;

    public AccessToken IssueUserToken(Guid userId, Guid tenantId, Guid sessionId, DateTimeOffset now)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("principal_type", "user"),
            new Claim("sid", sessionId.ToString())
        };
        return IssueToken(claims, now);
    }

    public AccessToken IssueServiceAccountToken(Guid tenantId, string clientId, DateTimeOffset now)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, clientId),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("principal_type", "service_account"),
            new Claim("client_id", clientId)
        };
        return IssueToken(claims, now);
    }

    private AccessToken IssueToken(IReadOnlyCollection<Claim> claims, DateTimeOffset now)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.SigningKey))
        {
            throw new InvalidOperationException(
                "Authentication:Local:SigningKey must be configured to issue access tokens.");
        }

        var key = new SymmetricSecurityKey(Convert.FromBase64String(settings.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = now.AddMinutes(Math.Max(1, settings.AccessTokenLifetimeMinutes));
        var allClaims = claims.Append(new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()));

        var token = new JwtSecurityToken(
            settings.Issuer,
            settings.Audience,
            allClaims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AccessToken(_handler.WriteToken(token), expiresAt);
    }
}
