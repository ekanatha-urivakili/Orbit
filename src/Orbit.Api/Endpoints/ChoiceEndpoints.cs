using MediatR;
using Orbit.Application.Choices;

namespace Orbit.Api.Endpoints;

public static class ChoiceEndpoints
{
    public static RouteGroupBuilder MapChoiceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/choices", async (ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetSystemChoicesQuery(), cancellationToken)))
            .AllowAnonymous()
            .WithName("GetSystemChoices")
            .WithTags("Choices");
        return group;
    }
}
