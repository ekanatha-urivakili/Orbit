using MediatR;
using Orbit.Application.Access;
using Orbit.Domain.Access;

namespace Orbit.Api.Endpoints;

public static class RoleEndpoints
{
    public static RouteGroupBuilder MapRoleEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/roles", async (
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListRolesQuery(), cancellationToken)))
            .WithName("ListRoles")
            .WithTags("Roles");

        group.MapPost("/roles", async (
            CreateRoleRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var role = await sender.Send(new CreateRoleCommand(request.Name, request.Permissions), cancellationToken);
            return Results.Created($"/api/v1/roles/{role.Id}", role);
        })
        .WithName("CreateRole")
        .WithTags("Roles");

        group.MapPatch("/roles/{roleId:guid}", async (
            Guid roleId,
            RenameRoleRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new RenameRoleCommand(roleId, request.Name), cancellationToken)))
            .WithName("RenameRole")
            .WithTags("Roles");

        group.MapPut("/roles/{roleId:guid}/permissions", async (
            Guid roleId,
            UpdateRolePermissionsRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new UpdateRolePermissionsCommand(roleId, request.Permissions), cancellationToken)))
            .WithName("UpdateRolePermissions")
            .WithTags("Roles");

        group.MapDelete("/roles/{roleId:guid}", async (
            Guid roleId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteRoleCommand(roleId), cancellationToken);
            return Results.NoContent();
        })
        .WithName("DeleteRole")
        .WithTags("Roles");

        return group;
    }

    public sealed record CreateRoleRequest(string Name, IReadOnlyList<ProjectPermission> Permissions);

    public sealed record RenameRoleRequest(string Name);

    public sealed record UpdateRolePermissionsRequest(IReadOnlyList<ProjectPermission> Permissions);
}
