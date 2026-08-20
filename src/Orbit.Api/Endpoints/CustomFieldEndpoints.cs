using MediatR;
using Orbit.Application.Configuration;
using Orbit.Domain.Configuration;

namespace Orbit.Api.Endpoints;

public static class CustomFieldEndpoints
{
    public static RouteGroupBuilder MapCustomFieldEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/projects/{projectId:guid}/custom-fields", async (
            Guid projectId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListCustomFieldsQuery(projectId), cancellationToken)))
            .WithName("ListCustomFields")
            .WithTags("Configuration");

        group.MapPost("/projects/{projectId:guid}/custom-fields", async (
            Guid projectId,
            CreateCustomFieldRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var definition = await sender.Send(
                new CreateCustomFieldCommand(
                    projectId,
                    request.Key,
                    request.Label,
                    request.FieldType,
                    request.Required,
                    request.Order,
                    request.ChoiceOptions ?? []),
                cancellationToken);
            return Results.Created(
                $"/api/v1/projects/{projectId}/custom-fields/{definition.Id}", definition);
        })
        .WithName("CreateCustomField")
        .WithTags("Configuration");

        group.MapPatch("/projects/{projectId:guid}/custom-fields/{id:guid}", async (
            Guid projectId,
            Guid id,
            UpdateCustomFieldRequest request,
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
                new UpdateCustomFieldCommand(
                    projectId,
                    id,
                    request.Label,
                    request.Required,
                    request.Order,
                    request.Enabled,
                    request.ChoiceOptions ?? [],
                    version),
                cancellationToken);
            response.Headers.ETag = $"\"{definition.Version}\"";
            return Results.Ok(definition);
        })
        .WithName("UpdateCustomField")
        .WithTags("Configuration");

        return group;
    }

    public sealed record CreateCustomFieldRequest(
        string Key,
        string Label,
        CustomFieldType FieldType,
        bool Required,
        int Order,
        IReadOnlyList<CustomFieldChoiceOptionInput>? ChoiceOptions);

    public sealed record UpdateCustomFieldRequest(
        string Label,
        bool Required,
        int Order,
        bool Enabled,
        IReadOnlyList<CustomFieldChoiceOptionInput>? ChoiceOptions);
}
