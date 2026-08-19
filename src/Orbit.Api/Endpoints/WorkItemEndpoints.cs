using MediatR;
using Orbit.Application.Integrations;
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
                request.StartDate,
                request.TeamId,
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
                request.StartDate,
                request.TeamId,
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

        group.MapPatch("/work-items/{workItemId:guid}/type", async (
            Guid workItemId,
            ChangeWorkItemTypeRequest request,
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
                new ChangeWorkItemTypeCommand(workItemId, request.Type, expectedVersion),
                cancellationToken);
            httpResponse.Headers.ETag = $"\"{workItem.Version}\"";
            return Results.Ok(workItem);
        })
        .WithName("ChangeWorkItemType")
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

        group.MapGet("/work-items/{workItemId:guid}/history", async (
            Guid workItemId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ListWorkItemHistoryQuery(workItemId), cancellationToken);
            return Results.Ok(result);
        })
            .WithName("ListWorkItemHistory")
            .WithTags("Work items");

        // ------------------------------------------------------------------
        // Watchers
        // ------------------------------------------------------------------

        group.MapGet("/work-items/{workItemId:guid}/watchers", async (
            Guid workItemId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetWorkItemWatchersQuery(workItemId), cancellationToken)))
            .WithName("GetWorkItemWatchers")
            .WithTags("Work items");

        group.MapPut("/work-items/{workItemId:guid}/watchers/me", async (
            Guid workItemId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new WatchWorkItemCommand(workItemId), cancellationToken);
            return Results.NoContent();
        })
            .WithName("WatchWorkItem")
            .WithTags("Work items");

        group.MapDelete("/work-items/{workItemId:guid}/watchers/me", async (
            Guid workItemId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UnwatchWorkItemCommand(workItemId), cancellationToken);
            return Results.NoContent();
        })
            .WithName("UnwatchWorkItem")
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

        // ------------------------------------------------------------------
        // Share
        // ------------------------------------------------------------------

        group.MapPost("/work-items/{workItemId:guid}/share", async (
            Guid workItemId,
            ShareWorkItemRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(
                new ShareWorkItemCommand(
                    workItemId, request.MembershipIds, request.TeamIds, request.Message),
                cancellationToken);
            return Results.NoContent();
        })
            .WithName("ShareWorkItem")
            .WithTags("Work items");

        group.MapPost("/work-items/{workItemId:guid}/slack-share", async (
            Guid workItemId,
            SlackShareRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new PostWorkItemToSlackCommand(workItemId, request.Message), cancellationToken);
            return Results.NoContent();
        })
            .WithName("PostWorkItemToSlack")
            .WithTags("Work items");

        // ------------------------------------------------------------------
        // Flag / Cover
        // ------------------------------------------------------------------

        group.MapPatch("/work-items/{workItemId:guid}/flag", async (
            Guid workItemId,
            ToggleFlagRequest request,
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
                new ToggleWorkItemFlagCommand(workItemId, request.Flagged, expectedVersion), cancellationToken);
            httpResponse.Headers.ETag = $"\"{workItem.Version}\"";
            return Results.Ok(workItem);
        })
            .WithName("ToggleWorkItemFlag")
            .WithTags("Work items");

        group.MapPatch("/work-items/{workItemId:guid}/cover", async (
            Guid workItemId,
            SetCoverRequest request,
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
                new SetWorkItemCoverCommand(workItemId, request.AttachmentId, expectedVersion), cancellationToken);
            httpResponse.Headers.ETag = $"\"{workItem.Version}\"";
            return Results.Ok(workItem);
        })
            .WithName("SetWorkItemCover")
            .WithTags("Work items");

        // ------------------------------------------------------------------
        // Votes
        // ------------------------------------------------------------------

        group.MapGet("/work-items/{workItemId:guid}/votes", async (
            Guid workItemId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetWorkItemVotesQuery(workItemId), cancellationToken)))
            .WithName("GetWorkItemVotes")
            .WithTags("Work items");

        group.MapPut("/work-items/{workItemId:guid}/votes/me", async (
            Guid workItemId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new AddWorkItemVoteCommand(workItemId), cancellationToken);
            return Results.NoContent();
        })
            .WithName("AddWorkItemVote")
            .WithTags("Work items");

        group.MapDelete("/work-items/{workItemId:guid}/votes/me", async (
            Guid workItemId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new RemoveWorkItemVoteCommand(workItemId), cancellationToken);
            return Results.NoContent();
        })
            .WithName("RemoveWorkItemVote")
            .WithTags("Work items");

        // ------------------------------------------------------------------
        // Worklogs ("Log work")
        // ------------------------------------------------------------------

        group.MapGet("/work-items/{workItemId:guid}/worklogs", async (
            Guid workItemId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListWorklogsQuery(workItemId), cancellationToken)))
            .WithName("ListWorklogs")
            .WithTags("Work items");

        group.MapPost("/work-items/{workItemId:guid}/worklogs", async (
            Guid workItemId,
            AddWorklogRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var worklog = await sender.Send(
                new AddWorklogCommand(workItemId, request.MinutesSpent, request.WorkDate, request.Description),
                cancellationToken);
            return Results.Created($"/api/v1/work-items/{workItemId}/worklogs/{worklog.Id}", worklog);
        })
            .WithName("AddWorklog")
            .WithTags("Work items");

        group.MapDelete("/work-items/{workItemId:guid}/worklogs/{worklogId:guid}", async (
            Guid workItemId,
            Guid worklogId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteWorklogCommand(workItemId, worklogId), cancellationToken);
            return Results.NoContent();
        })
            .WithName("DeleteWorklog")
            .WithTags("Work items");

        // ------------------------------------------------------------------
        // Clone / Move / Archive / Delete / Export
        // ------------------------------------------------------------------

        group.MapPost("/work-items/{workItemId:guid}/clone", async (
            Guid workItemId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var clone = await sender.Send(new CloneWorkItemCommand(workItemId), cancellationToken);
            return Results.Created($"/api/v1/work-items/{clone.Id}", clone);
        })
            .WithName("CloneWorkItem")
            .WithTags("Work items");

        group.MapPost("/work-items/{workItemId:guid}/move", async (
            Guid workItemId,
            MoveWorkItemRequest request,
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
                new MoveWorkItemCommand(workItemId, request.TargetProjectId, expectedVersion), cancellationToken);
            httpResponse.Headers.ETag = $"\"{workItem.Version}\"";
            return Results.Ok(workItem);
        })
            .WithName("MoveWorkItem")
            .WithTags("Work items");

        group.MapPost("/work-items/{workItemId:guid}/archive", async (
            Guid workItemId,
            HttpRequest httpRequest,
            HttpResponse httpResponse,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!SettingsEndpoints.TryParseVersion(httpRequest.Headers.IfMatch, allowZero: false, out var expectedVersion))
            {
                return SettingsEndpoints.PreconditionRequired();
            }

            var workItem = await sender.Send(new ArchiveWorkItemCommand(workItemId, expectedVersion), cancellationToken);
            httpResponse.Headers.ETag = $"\"{workItem.Version}\"";
            return Results.Ok(workItem);
        })
            .WithName("ArchiveWorkItem")
            .WithTags("Work items");

        group.MapPost("/work-items/{workItemId:guid}/unarchive", async (
            Guid workItemId,
            HttpRequest httpRequest,
            HttpResponse httpResponse,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!SettingsEndpoints.TryParseVersion(httpRequest.Headers.IfMatch, allowZero: false, out var expectedVersion))
            {
                return SettingsEndpoints.PreconditionRequired();
            }

            var workItem = await sender.Send(new UnarchiveWorkItemCommand(workItemId, expectedVersion), cancellationToken);
            httpResponse.Headers.ETag = $"\"{workItem.Version}\"";
            return Results.Ok(workItem);
        })
            .WithName("UnarchiveWorkItem")
            .WithTags("Work items");

        group.MapDelete("/work-items/{workItemId:guid}", async (
            Guid workItemId,
            HttpRequest httpRequest,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!SettingsEndpoints.TryParseVersion(httpRequest.Headers.IfMatch, allowZero: false, out var expectedVersion))
            {
                return SettingsEndpoints.PreconditionRequired();
            }

            await sender.Send(new DeleteWorkItemCommand(workItemId, expectedVersion), cancellationToken);
            return Results.NoContent();
        })
            .WithName("DeleteWorkItem")
            .WithTags("Work items");

        group.MapGet("/work-items/{workItemId:guid}/export", async (
            Guid workItemId,
            WorkItemExportFormat format,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ExportWorkItemQuery(workItemId, format), cancellationToken);
            return Results.File(result.Content, result.ContentType, result.FileName);
        })
            .WithName("ExportWorkItem")
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
        DateOnly? StartDate,
        Guid? TeamId,
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
        DateOnly? StartDate,
        Guid? TeamId,
        decimal? StoryPoints,
        string[]? Labels,
        string[]? Countries,
        string[]? AttachmentNames);

    public sealed record ChangeStatusRequest(WorkItemStatus Status);

    public sealed record ChangeWorkItemTypeRequest(WorkItemType Type);

    public sealed record ReorderWorkItemRequest(Guid? BeforeWorkItemId, Guid? AfterWorkItemId);

    public sealed record AddWorkItemLinkRequest(WorkItemLinkKind Kind, Guid TargetWorkItemId, bool Inverse);

    public sealed record AddWorkItemCommentRequest(string Body);

    public sealed record EditWorkItemCommentRequest(string Body);

    public sealed record PresignWorkItemAttachmentUploadRequest(string FileName, string ContentType, long SizeBytes);

    public sealed record ConfirmWorkItemAttachmentRequest(
        string FileName, string ContentType, long SizeBytes, string ObjectKey);

    public sealed record ToggleFlagRequest(bool Flagged);

    public sealed record SetCoverRequest(Guid? AttachmentId);

    public sealed record AddWorklogRequest(int MinutesSpent, DateOnly WorkDate, string? Description);

    public sealed record MoveWorkItemRequest(Guid TargetProjectId);

    public sealed record ShareWorkItemRequest(Guid[] MembershipIds, Guid[] TeamIds, string? Message);

    public sealed record SlackShareRequest(string? Message);
}
