using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Orbit.Application.Abstractions;

namespace Orbit.Infrastructure.Identity;

internal sealed class Argon2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MemorySizeKb = 65_536;
    private const int Iterations = 3;
    private const int Parallelism = 2;
    private const int ParametersVersion = 1;

    // Fixed cost-equivalent inputs used when no real hash exists (unknown account / missing
    // credential) so VerifyAsync always does the same amount of Argon2 work and login timing
    // cannot be used to enumerate accounts (NFR-17).
    private static readonly byte[] DummySalt = RandomNumberGenerator.GetBytes(SaltSize);
    private static readonly byte[] DummyHash = RandomNumberGenerator.GetBytes(HashSize);

    public async Task<PasswordHash> HashAsync(string password, CancellationToken cancellationToken)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = MemorySizeKb,
            Iterations = Iterations,
            DegreeOfParallelism = Parallelism
        };
        var hash = await argon2.GetBytesAsync(HashSize).WaitAsync(cancellationToken);
        var encoded = $"$argon2id$v=19$m={MemorySizeKb},t={Iterations},p={Parallelism}$" +
            $"{Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        return new PasswordHash(encoded, "Argon2id", ParametersVersion);
    }

    public async Task<bool> VerifyAsync(string password, string? encodedHash, CancellationToken cancellationToken)
    {
        var parsed = encodedHash is null ? null : TryParse(encodedHash);
        var salt = parsed?.Salt ?? DummySalt;
        var expectedHash = parsed?.Hash ?? DummyHash;

        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = parsed?.MemorySize ?? MemorySizeKb,
            Iterations = parsed?.Iterations ?? Iterations,
            DegreeOfParallelism = parsed?.Parallelism ?? Parallelism
        };
        var computedHash = await argon2.GetBytesAsync(expectedHash.Length).WaitAsync(cancellationToken);

        // Compare unconditionally (fixed time) before folding in whether parsing even succeeded,
        // so a malformed stored hash does not short-circuit faster than a well-formed mismatch.
        var matches = CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
        return parsed is not null && matches;
    }

    private static ParsedHash? TryParse(string encoded)
    {
        try
        {
            var parts = encoded.Split('$', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5 || parts[0] != "argon2id")
            {
                return null;
            }

            var costParts = parts[2].Split(',');
            if (costParts.Length != 3)
            {
                return null;
            }

            var memorySize = int.Parse(costParts[0].Split('=')[1], CultureInfo.InvariantCulture);
            var iterations = int.Parse(costParts[1].Split('=')[1], CultureInfo.InvariantCulture);
            var parallelism = int.Parse(costParts[2].Split('=')[1], CultureInfo.InvariantCulture);
            var salt = Convert.FromBase64String(parts[3]);
            var hash = Convert.FromBase64String(parts[4]);
            return new ParsedHash(memorySize, iterations, parallelism, salt, hash);
        }
        catch (Exception exception) when (exception is FormatException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    private sealed record ParsedHash(int MemorySize, int Iterations, int Parallelism, byte[] Salt, byte[] Hash);
}
