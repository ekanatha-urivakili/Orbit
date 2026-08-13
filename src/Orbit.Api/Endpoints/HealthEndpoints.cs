using Microsoft.EntityFrameworkCore;
using Orbit.Infrastructure.Persistence;

namespace Orbit.Api.Endpoints;

public static class HealthEndpoints
{
    public static async Task<IResult> ReadyAsync(
        OrbitDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connected = await dbContext.Database.CanConnectAsync(cancellationToken);
        return connected
            ? Results.Ok(new { status = "ready" })
            : Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Database unavailable");
    }
}
