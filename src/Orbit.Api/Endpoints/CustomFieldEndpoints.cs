using MediatR;
using Orbit.Application.Configuration;
using Orbit.Domain.Configuration;

namespace Orbit.Api.Endpoints;

public static class CustomFieldEndpoints
{
    public static RouteGroupBuilder MapCustomFieldEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/custom-fields", async (
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListCustomFieldsQuery(), cancellationToken)))
            .WithName("ListCustomFields")
            .WithTags("Configuration");

        group.MapPost("/custom-fields", async (
            CreateCustomFieldRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var definition = await sender.Send(
                new CreateCustomFieldCommand(
                    request.Key, request.Label, request.FieldType, request.Required, request.Order),
                cancellationToken);
            return Results.Created($"/api/v1/custom-fields/{definition.Id}", definition);
        })
        .WithName("CreateCustomField")
        .WithTags("Configuration");

        group.MapPatch("/custom-fields/{id:guid}", async (
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
                new UpdateCustomFieldCommand(id, request.Label, request.Required, request.Order, request.Enabled, version),
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
        int Order);

    public sealed record UpdateCustomFieldRequest(string Label, bool Required, int Order, bool Enabled);
}
