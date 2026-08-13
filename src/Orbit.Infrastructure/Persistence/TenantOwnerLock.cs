using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;

namespace Orbit.Infrastructure.Persistence;

internal sealed class TenantOwnerLock(OrbitDbContext dbContext) : ITenantOwnerLock
{
    public async Task AcquireAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({tenantId.ToString()}, 0))",
            cancellationToken);
}
