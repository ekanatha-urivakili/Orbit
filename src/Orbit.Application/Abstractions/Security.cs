namespace Orbit.Application.Abstractions;

/// <summary>
/// Reversibly protects a secret (e.g. a third-party webhook URL) for storage at rest. Unlike the
/// one-way password/token hashing used elsewhere (Argon2, SHA-256 handoff codes), values protected
/// here must be read back in plaintext to be used against an external API.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedValue);
}
