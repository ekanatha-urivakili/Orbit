using MediatR;
using Orbit.Application.WorkItems;
using Orbit.Domain.Choices;

namespace Orbit.Api.Endpoints;

public static class WorkItemEndpoints
{
    public static RouteGroupBuilder MapWorkItemEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/work-items", async (
            Guid projectId,
            int? skip,
            int? take,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = take.HasValue
                ? new ListWorkItemsQuery(projectId, skip ?? 0, take.Value)
                : new ListWorkItemsQuery(projectId, skip ?? 0);
            return Results.Ok(await sender.Send(query, cancellationToken));
        })
            .WithName("ListWorkItems")
            .WithTags("Work items");

        group.MapPost("/work-items", async (
            CreateWorkItemRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateWorkItemCommand(
                request.ProjectId,
                request.Summary,
                request.Description,
                request.Type,
                request.Priority,
                request.ParentId,
                request.EpicName,
                request.AcceptanceCriteria,
                request.StepsToConduct,
                request.AssigneeUserId,
                request.DeveloperUserId,
                request.ProductOwnerUserId,
                request.SprintName,
                request.IdentifiedOn,
                request.StoryPoints,
                request.LinkType,
                request.LinkedWorkItemId,
                request.Labels,
                request.Countries,
                request.AttachmentNames);
            var workItem = await sender.Send(command, cancellationToken);
            return Results.Created($"/api/v1/work-items/{workItem.Id}", workItem);
        })
        .WithName("CreateWorkItem")
        .WithTags("Work items");

        group.MapPatch("/work-items/{workItemId:guid}", async (
            Guid workItemId,
            UpdateWorkItemRequest request,
            HttpRequest httpRequest,
            HttpResponse httpResponse,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!SettingsEndpoints.TryParseVersion(httpRequest.Headers.IfMatch, allowZero: false, out var expectedVersion))
            {
                return SettingsEndpoints.PreconditionRequired();
            }

            var command = new UpdateWorkItemCommand(
                workItemId,
                request.Summary,
                request.Description,
                request.Priority,
                request.ParentId,
                request.EpicName,
                request.AcceptanceCriteria,
                request.StepsToConduct,
                request.AssigneeUserId,
                request.DeveloperUserId,
                request.ProductOwnerUserId,
                request.SprintName,
                request.IdentifiedOn,
                request.StoryPoints,
                request.LinkType,
                request.LinkedWorkItemId,
                request.Labels,
                request.Countries,
                request.AttachmentNames,
                expectedVersion);
            var workItem = await sender.Send(command, cancellationToken);
            httpResponse.Headers.ETag = $"\"{workItem.Version}\"";
            return Results.Ok(workItem);
        })
        .WithName("UpdateWorkItem")
        .WithTags("Work items");

        group.MapPatch("/work-items/{workItemId:guid}/status", async (
            Guid workItemId,
            ChangeStatusRequest request,
            HttpRequest httpRequest,
            HttpResponse httpResponse,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!SettingsEndpoints.TryParseVersion(httpRequest.Headers.IfMatch, allowZero: false, out var expectedVersion))
            {
                return SettingsEndpoints.PreconditionRequired();
            }

            var workItem = await sender.Send(
                new ChangeWorkItemStatusCommand(workItemId, request.Status, expectedVersion),
                cancellationToken);
            httpResponse.Headers.ETag = $"\"{workItem.Version}\"";
            return Results.Ok(workItem);
        })
        .WithName("ChangeWorkItemStatus")
        .WithTags("Work items");

        group.MapPatch("/work-items/{workItemId:guid}/rank", async (
            Guid workItemId,
            ReorderWorkItemRequest request,
            HttpRequest httpRequest,
            HttpResponse httpResponse,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!SettingsEndpoints.TryParseVersion(httpRequest.Headers.IfMatch, allowZero: false, out var expectedVersion))
            {
                return SettingsEndpoints.PreconditionRequired();
            }

            var workItem = await sender.Send(
                new ReorderWorkItemCommand(workItemId, request.BeforeWorkItemId, request.AfterWorkItemId, expectedVersion),
                cancellationToken);
            httpResponse.Headers.ETag = $"\"{workItem.Version}\"";
            return Results.Ok(workItem);
        })
        .WithName("ReorderWorkItem")
        .WithTags("Work items");

        return group;
    }

    public sealed record CreateWorkItemRequest(
        Guid ProjectId,
        string Summary,
        string? Description,
        WorkItemType Type,
        Priority Priority,
        Guid? ParentId,
        string? EpicName,
        string? AcceptanceCriteria,
        string? StepsToConduct,
        Guid? AssigneeUserId,
        Guid? DeveloperUserId,
        Guid? ProductOwnerUserId,
        string? SprintName,
        string? IdentifiedOn,
        decimal? StoryPoints,
        WorkItemLinkType? LinkType,
        Guid? LinkedWorkItemId,
        string[]? Labels,
        string[]? Countries,
        string[]? AttachmentNames);

    public sealed record UpdateWorkItemRequest(
        string Summary,
        string? Description,
        Priority Priority,
        Guid? ParentId,
        string? EpicName,
        string? AcceptanceCriteria,
        string? StepsToConduct,
        Guid? AssigneeUserId,
        Guid? DeveloperUserId,
        Guid? ProductOwnerUserId,
        string? SprintName,
        string? IdentifiedOn,
        decimal? StoryPoints,
        WorkItemLinkType? LinkType,
        Guid? LinkedWorkItemId,
        string[]? Labels,
        string[]? Countries,
        string[]? AttachmentNames);

    public sealed record ChangeStatusRequest(WorkItemStatus Status);

    public sealed record ReorderWorkItemRequest(Guid? BeforeWorkItemId, Guid? AfterWorkItemId);
}
