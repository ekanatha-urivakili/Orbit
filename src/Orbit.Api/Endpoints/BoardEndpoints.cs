using MediatR;
using Orbit.Application.Boards;
using Orbit.Domain.Choices;

namespace Orbit.Api.Endpoints;

public static class BoardEndpoints
{
    public static RouteGroupBuilder MapBoardEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/projects/{projectId:guid}/board", async (
            Guid projectId,
            HttpResponse response,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var board = await sender.Send(new GetBoardQuery(projectId), cancellationToken);
            response.Headers.ETag = $"\"{board.Version}\"";
            return Results.Ok(board);
        })
        .WithName("GetProjectBoard")
        .WithTags("Boards");

        group.MapPatch("/projects/{projectId:guid}/board", async (
            Guid projectId,
            UpdateBoardRequest request,
            HttpRequest httpRequest,
            HttpResponse response,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!SettingsEndpoints.TryParseVersion(httpRequest.Headers.IfMatch, allowZero: true, out var version))
            {
                return SettingsEndpoints.PreconditionRequired();
            }

            var columns = request.Columns ?? [];
            var board = await sender.Send(
                new UpdateBoardCommand(projectId, request.Name, request.Type, columns, version),
                cancellationToken);
            response.Headers.ETag = $"\"{board.Version}\"";
            return Results.Ok(board);
        })
        .WithName("UpdateProjectBoard")
        .WithTags("Boards");

        return group;
    }

    public sealed record UpdateBoardRequest(
        string Name,
        BoardType Type,
        IReadOnlyList<UpdateBoardColumnInput>? Columns);
}
