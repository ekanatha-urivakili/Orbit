using MediatR;
using Orbit.Application.Directory;

namespace Orbit.Api.Endpoints;

public static class TeamEndpoints
{
    public static RouteGroupBuilder MapTeamEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/teams", async (
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListTeamsQuery(), cancellationToken)))
            .WithName("ListTeams")
            .WithTags("Teams");

        group.MapPost("/teams", async (
            CreateTeamRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var team = await sender.Send(new CreateTeamCommand(request.Name), cancellationToken);
            return Results.Created($"/api/v1/teams/{team.Id}", team);
        })
        .WithName("CreateTeam")
        .WithTags("Teams");

        group.MapPut("/teams/{teamId:guid}", async (
            Guid teamId,
            RenameTeamRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new RenameTeamCommand(teamId, request.Name), cancellationToken)))
            .WithName("RenameTeam")
            .WithTags("Teams");

        group.MapGet("/teams/{teamId:guid}/members", async (
            Guid teamId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListTeamMembersQuery(teamId), cancellationToken)))
            .WithName("ListTeamMembers")
            .WithTags("Teams");

        group.MapPost("/teams/{teamId:guid}/members", async (
            Guid teamId,
            AddTeamMemberRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var membership = await sender.Send(
                new AddTeamMemberCommand(teamId, request.MembershipId),
                cancellationToken);
            return Results.Created($"/api/v1/teams/{teamId}/members/{membership.MembershipId}", membership);
        })
        .WithName("AddTeamMember")
        .WithTags("Teams");

        group.MapDelete("/teams/{teamId:guid}/members/{membershipId:guid}", async (
            Guid teamId,
            Guid membershipId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new RemoveTeamMemberCommand(teamId, membershipId), cancellationToken);
            return Results.NoContent();
        })
        .WithName("RemoveTeamMember")
        .WithTags("Teams");

        return group;
    }

    public sealed record CreateTeamRequest(string Name);

    public sealed record RenameTeamRequest(string Name);

    public sealed record AddTeamMemberRequest(Guid MembershipId);
}
