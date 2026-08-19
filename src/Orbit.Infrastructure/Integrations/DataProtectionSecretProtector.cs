using Microsoft.AspNetCore.DataProtection;
using Orbit.Application.Abstractions;

namespace Orbit.Infrastructure.Integrations;

internal sealed class DataProtectionSecretProtector : ISecretProtector
{
    private readonly IDataProtector protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider) =>
        protector = provider.CreateProtector("Orbit.Integrations.Secrets.v1");

    public string Protect(string plaintext) => protector.Protect(plaintext);

    public string Unprotect(string protectedValue) => protector.Unprotect(protectedValue);
}
