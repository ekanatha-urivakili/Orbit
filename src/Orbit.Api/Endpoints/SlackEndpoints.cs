using MediatR;
using Orbit.Application.Integrations;

namespace Orbit.Api.Endpoints;

public static class SlackEndpoints
{
    public static RouteGroupBuilder MapSlackEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/integrations/slack/authorize-url", async (
            StartSlackConnectRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var url = await sender.Send(new StartSlackConnectCommand(request.ProjectId), cancellationToken);
            return Results.Ok(new { url });
        })
            .WithName("StartSlackConnect")
            .WithTags("Integrations");

        group.MapPost("/integrations/slack/complete", async (
            CompleteSlackOAuthRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new CompleteSlackOAuthCommand(request.Code, request.State), cancellationToken)))
            .WithName("CompleteSlackOAuth")
            .WithTags("Integrations");

        group.MapGet("/integrations/slack/connection", async (
            Guid projectId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetSlackConnectionQuery(projectId), cancellationToken)))
            .WithName("GetSlackConnection")
            .WithTags("Integrations");

        group.MapDelete("/integrations/slack/connections/{connectionId:guid}", async (
            Guid connectionId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new DisconnectSlackCommand(connectionId), cancellationToken);
            return Results.NoContent();
        })
            .WithName("DisconnectSlack")
            .WithTags("Integrations");

        return group;
    }

    public sealed record StartSlackConnectRequest(Guid ProjectId);

    public sealed record CompleteSlackOAuthRequest(string Code, string State);
}
