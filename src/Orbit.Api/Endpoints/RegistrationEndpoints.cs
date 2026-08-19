using MediatR;
using Orbit.Application.Organizations;

namespace Orbit.Api.Endpoints;

public static class RegistrationEndpoints
{
    public static RouteGroupBuilder MapRegistrationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/register", async (
            RegisterRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new SignUpCommand(
                    request.DisplayName,
                    request.Email,
                    request.Password,
                    request.OrganizationName,
                    request.WorkspaceName,
                    ClientContext.UserAgent(httpContext),
                    ClientContext.IpAddress(httpContext)),
                cancellationToken);
            return Results.Created($"/api/v1/workspaces/{result.WorkspaceId}", result);
        })
        .WithName("Register")
        .WithTags("Registration")
        .AllowAnonymous()
        .RequireRateLimiting("auth");

        return group;
    }

    public sealed record RegisterRequest(
        string DisplayName,
        string Email,
        string Password,
        string OrganizationName,
        string WorkspaceName);
}
