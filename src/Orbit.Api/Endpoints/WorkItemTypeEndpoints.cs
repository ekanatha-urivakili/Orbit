using MediatR;
using Orbit.Application.Configuration;
using Orbit.Domain.Choices;

namespace Orbit.Api.Endpoints;

public static class WorkItemTypeEndpoints
{
    public static RouteGroupBuilder MapWorkItemTypeEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/work-item-types", async (
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListWorkItemTypesQuery(), cancellationToken)))
            .WithName("ListWorkItemTypes")
            .WithTags("Configuration");

        group.MapPatch("/work-item-types/{id}", async (
            WorkItemType id,
            UpdateWorkItemTypeRequest request,
            HttpRequest httpRequest,
            HttpResponse response,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!SettingsEndpoints.TryParseVersion(httpRequest.Headers.IfMatch, allowZero: false, out var version))
            {
                return SettingsEndpoints.PreconditionRequired();
            }

            var definition = await sender.Send(
                new UpdateWorkItemTypeCommand(
                    id,
                    request.Label,
                    request.Description,
                    request.Order,
                    request.ColorToken,
                    request.Enabled,
                    version),
                cancellationToken);
            response.Headers.ETag = $"\"{definition.Version}\"";
            return Results.Ok(definition);
        })
        .WithName("UpdateWorkItemType")
        .WithTags("Configuration");

        return group;
    }

    public sealed record UpdateWorkItemTypeRequest(
        string Label,
        string Description,
        int Order,
        string ColorToken,
        bool Enabled);
}
