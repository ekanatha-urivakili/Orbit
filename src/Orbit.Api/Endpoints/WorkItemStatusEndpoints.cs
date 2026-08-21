using MediatR;
using Orbit.Application.Configuration;
using Orbit.Domain.Choices;

namespace Orbit.Api.Endpoints;

/// <summary>The "Edit workflow" / "Add status" endpoints backing a project's status catalog.</summary>
public static class WorkItemStatusEndpoints
{
    public static RouteGroupBuilder MapWorkItemStatusEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/projects/{projectId:guid}/statuses", async (
            Guid projectId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListWorkItemStatusesQuery(projectId), cancellationToken)))
            .WithName("ListWorkItemStatuses")
            .WithTags("Configuration");

        group.MapPost("/projects/{projectId:guid}/statuses", async (
            Guid projectId,
            CreateWorkItemStatusRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var definition = await sender.Send(
                new CreateWorkItemStatusCommand(
                    projectId, request.Key, request.Name, request.Category, request.Order, request.ColorToken),
                cancellationToken);
            return Results.Created($"/api/v1/projects/{projectId}/statuses/{definition.Id}", definition);
        })
        .WithName("CreateWorkItemStatus")
        .WithTags("Configuration");

        group.MapPatch("/projects/{projectId:guid}/statuses/{statusId:guid}", async (
            Guid projectId,
            Guid statusId,
            UpdateWorkItemStatusRequest request,
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
                new UpdateWorkItemStatusCommand(
                    projectId, statusId, request.Name, request.Category, request.Order, request.ColorToken, version),
                cancellationToken);
            response.Headers.ETag = $"\"{definition.Version}\"";
            return Results.Ok(definition);
        })
        .WithName("UpdateWorkItemStatus")
        .WithTags("Configuration");

        group.MapPost("/projects/{projectId:guid}/statuses/{statusId:guid}/default", async (
            Guid projectId,
            Guid statusId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new SetDefaultWorkItemStatusCommand(projectId, statusId), cancellationToken)))
        .WithName("SetDefaultWorkItemStatus")
        .WithTags("Configuration");

        group.MapDelete("/projects/{projectId:guid}/statuses/{statusId:guid}", async (
            Guid projectId,
            Guid statusId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteWorkItemStatusCommand(projectId, statusId), cancellationToken);
            return Results.NoContent();
        })
        .WithName("DeleteWorkItemStatus")
        .WithTags("Configuration");

        return group;
    }

    public sealed record CreateWorkItemStatusRequest(string Key, string Name, StatusCategory Category, int Order, string ColorToken);

    public sealed record UpdateWorkItemStatusRequest(string Name, StatusCategory Category, int Order, string ColorToken);
}
