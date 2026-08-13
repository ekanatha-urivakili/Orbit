using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Orbit.Application.Abstractions;

namespace Orbit.Infrastructure.Persistence;

public sealed class OrbitDbContextFactory : IDesignTimeDbContextFactory<OrbitDbContext>
{
    public OrbitDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PostgresAdmin")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=orbit;Username=orbit;Password=orbit_local";
        var options = new DbContextOptionsBuilder<OrbitDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new OrbitDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
    }
}
