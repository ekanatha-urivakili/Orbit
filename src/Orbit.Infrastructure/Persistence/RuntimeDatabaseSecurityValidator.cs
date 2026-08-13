using Microsoft.EntityFrameworkCore;

namespace Orbit.Infrastructure.Persistence;

public sealed class RuntimeDatabaseSecurityValidator(OrbitDbContext dbContext)
{
    public async Task ValidateAsync(CancellationToken cancellationToken)
    {
        var privileged = await dbContext.Database
            .SqlQueryRaw<bool>(
                "SELECT (rolsuper OR rolbypassrls) AS \"Value\" FROM pg_roles WHERE rolname = current_user")
            .SingleAsync(cancellationToken);
        if (privileged)
        {
            throw new InvalidOperationException(
                "The runtime PostgreSQL role must be NOSUPERUSER and NOBYPASSRLS.");
        }
    }
}
