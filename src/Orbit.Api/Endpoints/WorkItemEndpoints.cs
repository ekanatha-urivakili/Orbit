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

        // ------------------------------------------------------------------
        // Links
        // ------------------------------------------------------------------

        group.MapGet("/work-items/{workItemId:guid}/links", async (
            Guid workItemId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListWorkItemLinksQuery(workItemId), cancellationToken)))
            .WithName("ListWorkItemLinks")
            .WithTags("Work items");

        group.MapPost("/work-items/{workItemId:guid}/links", async (
            Guid workItemId,
            AddWorkItemLinkRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var link = await sender.Send(
                new AddWorkItemLinkCommand(workItemId, request.Kind, request.TargetWorkItemId, request.Inverse),
                cancellationToken);
            return Results.Created($"/api/v1/work-items/{workItemId}/links/{link.Id}", link);
        })
            .WithName("AddWorkItemLink")
            .WithTags("Work items");

        group.MapDelete("/work-items/{workItemId:guid}/links/{linkId:guid}", async (
            Guid workItemId,
            Guid linkId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new RemoveWorkItemLinkCommand(workItemId, linkId), cancellationToken);
            return Results.NoContent();
        })
            .WithName("RemoveWorkItemLink")
            .WithTags("Work items");

        // ------------------------------------------------------------------
        // Comments (E2.3 S2.3.1)
        // ------------------------------------------------------------------

        group.MapGet("/work-items/{workItemId:guid}/comments", async (
            Guid workItemId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ListWorkItemCommentsQuery(workItemId), cancellationToken);
            return Results.Ok(result);
        })
            .WithName("ListWorkItemComments")
            .WithTags("Work items");

        group.MapPost("/work-items/{workItemId:guid}/comments", async (
            Guid workItemId,
            AddWorkItemCommentRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            // SEC-10: Early validation at the endpoint layer protects the pipeline
            // from handling excessively large strings before MediatR/DB execution.
            if (request.Body is null || request.Body.Length > 15_000)
            {
                return Results.BadRequest("Comment body cannot exceed 15,000 characters.");
            }

            var comment = await sender.Send(
                new AddWorkItemCommentCommand(workItemId, request.Body),
                cancellationToken);
            return Results.Created(
                $"/api/v1/work-items/{workItemId}/comments/{comment.Id}",
                comment);
        })
            .WithName("AddWorkItemComment")
            .WithTags("Work items");

        group.MapPatch("/work-items/{workItemId:guid}/comments/{commentId:guid}", async (
            Guid workItemId,
            Guid commentId,
            EditWorkItemCommentRequest request,
            HttpRequest httpRequest,
            HttpResponse httpResponse,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!SettingsEndpoints.TryParseVersion(httpRequest.Headers.IfMatch, allowZero: false, out var expectedVersion))
            {
                return SettingsEndpoints.PreconditionRequired();
            }

            // SEC-10: Early validation at the endpoint layer for updates.
            if (request.Body is null || request.Body.Length > 15_000)
            {
                return Results.BadRequest("Comment body cannot exceed 15,000 characters.");
            }

            var comment = await sender.Send(
                new EditWorkItemCommentCommand(workItemId, commentId, request.Body, expectedVersion),
                cancellationToken);
            httpResponse.Headers.ETag = $"\"{comment.Version}\"";
            return Results.Ok(comment);
        })
            .WithName("EditWorkItemComment")
            .WithTags("Work items");

        group.MapDelete("/work-items/{workItemId:guid}/comments/{commentId:guid}", async (
            Guid workItemId,
            Guid commentId,
            HttpRequest httpRequest,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!SettingsEndpoints.TryParseVersion(httpRequest.Headers.IfMatch, allowZero: false, out var expectedVersion))
            {
                return SettingsEndpoints.PreconditionRequired();
            }

            await sender.Send(
                new DeleteWorkItemCommentCommand(workItemId, commentId, expectedVersion),
                cancellationToken);
            return Results.NoContent();
        })
            .WithName("DeleteWorkItemComment")
            .WithTags("Work items");

        // ------------------------------------------------------------------
        // Attachments
        // ------------------------------------------------------------------

        group.MapGet("/work-items/{workItemId:guid}/attachments", async (
            Guid workItemId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListWorkItemAttachmentsQuery(workItemId), cancellationToken)))
            .WithName("ListWorkItemAttachments")
            .WithTags("Work items");

        group.MapPost("/work-items/{workItemId:guid}/attachments/presign", async (
            Guid workItemId,
            PresignWorkItemAttachmentUploadRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(
                new PresignWorkItemAttachmentUploadCommand(
                    workItemId, request.FileName, request.ContentType, request.SizeBytes),
                cancellationToken)))
            .WithName("PresignWorkItemAttachmentUpload")
            .WithTags("Work items");

        group.MapPost("/work-items/{workItemId:guid}/attachments", async (
            Guid workItemId,
            ConfirmWorkItemAttachmentRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var attachment = await sender.Send(
                new ConfirmWorkItemAttachmentCommand(
                    workItemId, request.FileName, request.ContentType, request.SizeBytes, request.ObjectKey),
                cancellationToken);
            return Results.Created(
                $"/api/v1/work-items/{workItemId}/attachments/{attachment.Id}",
                attachment);
        })
            .WithName("ConfirmWorkItemAttachment")
            .WithTags("Work items");

        group.MapDelete("/work-items/{workItemId:guid}/attachments/{attachmentId:guid}", async (
            Guid workItemId,
            Guid attachmentId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteWorkItemAttachmentCommand(workItemId, attachmentId), cancellationToken);
            return Results.NoContent();
        })
            .WithName("DeleteWorkItemAttachment")
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
        string[]? Labels,
        string[]? Countries,
        string[]? AttachmentNames);

    public sealed record ChangeStatusRequest(WorkItemStatus Status);

    public sealed record ReorderWorkItemRequest(Guid? BeforeWorkItemId, Guid? AfterWorkItemId);

    public sealed record AddWorkItemLinkRequest(WorkItemLinkKind Kind, Guid TargetWorkItemId, bool Inverse);

    public sealed record AddWorkItemCommentRequest(string Body);

    public sealed record EditWorkItemCommentRequest(string Body);

    public sealed record PresignWorkItemAttachmentUploadRequest(string FileName, string ContentType, long SizeBytes);

    public sealed record ConfirmWorkItemAttachmentRequest(
        string FileName, string ContentType, long SizeBytes, string ObjectKey);
}
