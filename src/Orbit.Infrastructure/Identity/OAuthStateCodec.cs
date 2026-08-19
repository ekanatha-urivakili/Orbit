using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Orbit.Application.Abstractions;

namespace Orbit.Infrastructure.Identity;

/// <summary>
/// Encodes the OAuth <c>state</c> parameter as a self-contained, HMAC-signed token (mode + nonce +
/// expiry) rather than a server-side session row - the callback that decodes it runs before any
/// authenticated context or database handoff row exists. Signed with the same key that signs local
/// access tokens (<see cref="LocalTokenOptions.SigningKey"/>): both are short-lived, server-only
/// secrets with no reason to be different keys.
/// </summary>
internal sealed class OAuthStateCodec(IOptions<LocalTokenOptions> localTokenOptions) : IOAuthStateCodec
{
    private sealed record StatePayload(string Mode, string Nonce, long ExpiresAtUnixSeconds, string? ReturnUrl = null);

    public string Encode(string mode, DateTimeOffset now, TimeSpan lifetime, string? returnUrl = null)
    {
        var payload = new StatePayload(
            mode,
            Convert.ToHexString(RandomNumberGenerator.GetBytes(8)),
            now.Add(lifetime).ToUnixTimeSeconds(),
            returnUrl);
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var payloadSegment = Base64UrlEncode(payloadBytes);
        var signature = Base64UrlEncode(Sign(payloadBytes));
        return $"{payloadSegment}.{signature}";
    }

    public bool TryDecode(string state, DateTimeOffset now, out string mode, out string? returnUrl)
    {
        mode = string.Empty;
        returnUrl = null;
        var parts = state.Split('.', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        byte[] payloadBytes;
        byte[] signature;
        try
        {
            payloadBytes = Base64UrlDecode(parts[0]);
            signature = Base64UrlDecode(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (!CryptographicOperations.FixedTimeEquals(signature, Sign(payloadBytes)))
        {
            return false;
        }

        StatePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<StatePayload>(payloadBytes);
        }
        catch (JsonException)
        {
            return false;
        }

        if (payload is null || DateTimeOffset.FromUnixTimeSeconds(payload.ExpiresAtUnixSeconds) <= now)
        {
            return false;
        }

        mode = payload.Mode;
        returnUrl = payload.ReturnUrl;
        return true;
    }

    private byte[] Sign(byte[] payload)
    {
        var key = string.IsNullOrWhiteSpace(localTokenOptions.Value.SigningKey)
            ? Encoding.UTF8.GetBytes(localTokenOptions.Value.Issuer)
            : Convert.FromBase64String(localTokenOptions.Value.SigningKey);
        return new HMACSHA256(key).ComputeHash(payload);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        var padded = normalized.PadRight(normalized.Length + ((4 - (normalized.Length % 4)) % 4), '=');
        return Convert.FromBase64String(padded);
    }
}
