using MediatR;
using Orbit.Application.Workspaces;

namespace Orbit.Api.Endpoints;

public static class WorkspaceEndpoints
{
    public static RouteGroupBuilder MapWorkspaceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/me/site-capabilities", async (
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetSiteCapabilitiesQuery(), cancellationToken)))
            .WithName("GetSiteCapabilities")
            .WithTags("Workspaces");

        group.MapPost("/workspaces", async (
            CreateWorkspaceRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var workspace = await sender.Send(
                new CreateWorkspaceCommand(request.Name),
                cancellationToken);
            return Results.Created($"/api/v1/workspaces/{workspace.Id}", workspace);
        })
        .WithName("CreateWorkspace")
        .WithTags("Workspaces");

        group.MapPost("/organization/workspaces", async (
            CreateWorkspaceRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var workspace = await sender.Send(
                new CreateWorkspaceInOrganizationCommand(request.Name),
                cancellationToken);
            return Results.Created($"/api/v1/workspaces/{workspace.Id}", workspace);
        })
        .WithName("CreateWorkspaceInOrganization")
        .WithTags("Workspaces");

        return group;
    }

    public sealed record CreateWorkspaceRequest(string Name);
}
