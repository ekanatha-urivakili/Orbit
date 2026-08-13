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

    public AccessToken IssueUserToken(Guid userId, Guid tenantId, Guid sessionId, DateTimeOffset now)
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
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("principal_type", "user"),
            new Claim("sid", sessionId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString())
        };

        var token = new JwtSecurityToken(
            settings.Issuer,
            settings.Audience,
            claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AccessToken(_handler.WriteToken(token), expiresAt);
    }
}
