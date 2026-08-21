using MediatR;
using Orbit.Application.Settings;
using Orbit.Domain.Settings;

namespace Orbit.Api.Endpoints;

/// <summary>The board "View settings" panel: per-user field visibility, column sizing, and hide-done-after.</summary>
public static class BoardViewPreferenceEndpoints
{
    public static RouteGroupBuilder MapBoardViewPreferenceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/projects/{projectId:guid}/board-view-preference", async (
            Guid projectId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetBoardViewPreferenceQuery(projectId), cancellationToken)))
            .WithName("GetBoardViewPreference")
            .WithTags("Boards");

        group.MapPatch("/projects/{projectId:guid}/board-view-preference", async (
            Guid projectId,
            UpdateBoardViewPreferenceRequest request,
            HttpRequest httpRequest,
            HttpResponse response,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!SettingsEndpoints.TryParseVersion(httpRequest.Headers.IfMatch, allowZero: true, out var version))
            {
                return SettingsEndpoints.PreconditionRequired();
            }

            var preference = await sender.Send(
                new UpdateBoardViewPreferenceCommand(
                    projectId, request.HideDoneItemsAfter, request.ColumnSizeMode, request.HiddenFields, version),
                cancellationToken);
            response.Headers.ETag = $"\"{preference.Version}\"";
            return Results.Ok(preference);
        })
        .WithName("UpdateBoardViewPreference")
        .WithTags("Boards");

        return group;
    }

    public sealed record UpdateBoardViewPreferenceRequest(
        HideDoneItemsAfter HideDoneItemsAfter,
        BoardColumnSizeMode ColumnSizeMode,
        IReadOnlyList<string> HiddenFields);
}
