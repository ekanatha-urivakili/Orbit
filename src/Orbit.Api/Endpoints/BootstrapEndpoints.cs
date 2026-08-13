using MediatR;
using Orbit.Application.Identity;

namespace Orbit.Api.Endpoints;

public static class BootstrapEndpoints
{
    public static RouteGroupBuilder MapBootstrapEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/bootstrap/status", async (
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetBootstrapStatusQuery(), cancellationToken)))
            .WithName("GetBootstrapStatus")
            .WithTags("Bootstrap")
            .AllowAnonymous();

        group.MapPost("/bootstrap", async (
            BootstrapRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new BootstrapCommand(
                    request.DisplayName,
                    request.Email,
                    request.Password,
                    request.WorkspaceName),
                cancellationToken);
            return Results.Created($"/api/v1/workspaces/{result.WorkspaceId}", result);
        })
        .WithName("BootstrapInstallation")
        .WithTags("Bootstrap")
        .AllowAnonymous()
        .RequireRateLimiting("bootstrap");

        return group;
    }

    public sealed record BootstrapRequest(
        string DisplayName,
        string Email,
        string Password,
        string WorkspaceName);
}
