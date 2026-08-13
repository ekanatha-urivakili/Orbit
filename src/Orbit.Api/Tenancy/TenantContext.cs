using Orbit.Application.Abstractions;

namespace Orbit.Api.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }

    public void SetTenant(Guid tenantId)
    {
        if (TenantId != Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is immutable for a request.");
        }

        TenantId = tenantId;
    }
}
