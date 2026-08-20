using MediatR;
using Orbit.Api.Idempotency;
using Orbit.Application.Projects;

namespace Orbit.Api.Endpoints;

public static class ProjectEndpoints
{
    public static RouteGroupBuilder MapProjectEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/projects", async (
            int? skip,
            int? take,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = take.HasValue
                ? new ListProjectsQuery(skip ?? 0, take.Value)
                : new ListProjectsQuery(skip ?? 0);
            return Results.Ok(await sender.Send(query, cancellationToken));
        })
            .WithName("ListProjects")
            .WithTags("Projects");

        group.MapPost("/projects", async (
            CreateProjectRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var project = await sender.Send(new CreateProjectCommand(request.Key, request.Name), cancellationToken);
            return Results.Created($"/api/v1/projects/{project.Id}", project);
        })
        .WithName("CreateProject")
        .WithTags("Projects")
        .AddEndpointFilter<IdempotencyKeyFilter>();

        return group;
    }

    public sealed record CreateProjectRequest(string Key, string Name);
}
