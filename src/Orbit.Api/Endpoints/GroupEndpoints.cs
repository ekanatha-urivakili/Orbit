using MediatR;
using Orbit.Application.Directory;

namespace Orbit.Api.Endpoints;

public static class GroupEndpoints
{
    public static RouteGroupBuilder MapGroupEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/groups", async (
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListGroupsQuery(), cancellationToken)))
            .WithName("ListGroups")
            .WithTags("Groups");

        group.MapPost("/groups", async (
            CreateGroupRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var directoryGroup = await sender.Send(new CreateGroupCommand(request.Name), cancellationToken);
            return Results.Created($"/api/v1/groups/{directoryGroup.Id}", directoryGroup);
        })
        .WithName("CreateGroup")
        .WithTags("Groups");

        group.MapPut("/groups/{groupId:guid}", async (
            Guid groupId,
            RenameGroupRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new RenameGroupCommand(groupId, request.Name), cancellationToken)))
            .WithName("RenameGroup")
            .WithTags("Groups");

        group.MapGet("/groups/{groupId:guid}/members", async (
            Guid groupId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListGroupMembersQuery(groupId), cancellationToken)))
            .WithName("ListGroupMembers")
            .WithTags("Groups");

        group.MapPost("/groups/{groupId:guid}/members", async (
            Guid groupId,
            AddGroupMemberRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var membership = await sender.Send(
                new AddGroupMemberCommand(groupId, request.MembershipId),
                cancellationToken);
            return Results.Created($"/api/v1/groups/{groupId}/members/{membership.MembershipId}", membership);
        })
        .WithName("AddGroupMember")
        .WithTags("Groups");

        group.MapDelete("/groups/{groupId:guid}/members/{membershipId:guid}", async (
            Guid groupId,
            Guid membershipId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new RemoveGroupMemberCommand(groupId, membershipId), cancellationToken);
            return Results.NoContent();
        })
        .WithName("RemoveGroupMember")
        .WithTags("Groups");

        return group;
    }

    public sealed record CreateGroupRequest(string Name);

    public sealed record RenameGroupRequest(string Name);

    public sealed record AddGroupMemberRequest(Guid MembershipId);
}
