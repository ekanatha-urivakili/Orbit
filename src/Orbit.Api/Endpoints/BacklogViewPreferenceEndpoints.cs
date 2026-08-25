using MediatR;
using Orbit.Application.Settings;
using Orbit.Domain.Settings;

namespace Orbit.Api.Endpoints;

/// <summary>The backlog "View settings" panel: per-user empty-sprint visibility, row density, and field visibility.</summary>
public static class BacklogViewPreferenceEndpoints
{
    public static RouteGroupBuilder MapBacklogViewPreferenceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/projects/{projectId:guid}/backlog-view-preference", async (
            Guid projectId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetBacklogViewPreferenceQuery(projectId), cancellationToken)))
            .WithName("GetBacklogViewPreference")
            .WithTags("Boards");

        group.MapPatch("/projects/{projectId:guid}/backlog-view-preference", async (
            Guid projectId,
            UpdateBacklogViewPreferenceRequest request,
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
                new UpdateBacklogViewPreferenceCommand(
                    projectId, request.ShowEmptySprints, request.Density, request.HiddenFields, version),
                cancellationToken);
            response.Headers.ETag = $"\"{preference.Version}\"";
            return Results.Ok(preference);
        })
        .WithName("UpdateBacklogViewPreference")
        .WithTags("Boards");

        return group;
    }

    public sealed record UpdateBacklogViewPreferenceRequest(
        bool ShowEmptySprints,
        BacklogRowDensity Density,
        IReadOnlyList<string> HiddenFields);
}
