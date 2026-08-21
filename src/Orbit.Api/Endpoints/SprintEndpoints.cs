using MediatR;
using Orbit.Api.Idempotency;
using Orbit.Application.Boards;

namespace Orbit.Api.Endpoints;

public static class SprintEndpoints
{
    public static RouteGroupBuilder MapSprintEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/projects/{projectId:guid}/sprints", async (
            Guid projectId,
            CreateSprintRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var sprint = await sender.Send(new CreateSprintCommand(projectId, request.Name), cancellationToken);
            return Results.Created($"/api/v1/sprints/{sprint.Id}", sprint);
        })
        .WithName("CreateSprint")
        .WithTags("Sprints")
        .AddEndpointFilter<IdempotencyKeyFilter>();

        group.MapGet("/projects/{projectId:guid}/sprints", async (
            Guid projectId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListSprintsQuery(projectId), cancellationToken)))
        .WithName("ListSprints")
        .WithTags("Sprints");

        group.MapPatch("/sprints/{sprintId:guid}", async (
            Guid sprintId,
            UpdateSprintRequest request,
            HttpRequest httpRequest,
            HttpResponse httpResponse,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!SettingsEndpoints.TryParseVersion(httpRequest.Headers.IfMatch, allowZero: false, out var expectedVersion))
            {
                return SettingsEndpoints.PreconditionRequired();
            }

            var sprint = await sender.Send(
                new UpdateSprintCommand(sprintId, request.Name, request.Goal, request.StartDate, request.EndDate, expectedVersion),
                cancellationToken);
            httpResponse.Headers.ETag = $"\"{sprint.Version}\"";
            return Results.Ok(sprint);
        })
        .WithName("UpdateSprint")
        .WithTags("Sprints");

        group.MapGet("/sprints/{sprintId:guid}/insights", async (
            Guid sprintId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new SprintInsightsQuery(sprintId), cancellationToken)))
        .WithName("GetSprintInsights")
        .WithTags("Sprints");

        group.MapPost("/sprints/{sprintId:guid}/start", async (
            Guid sprintId,
            StartSprintRequest request,
            HttpRequest httpRequest,
            HttpResponse httpResponse,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!SettingsEndpoints.TryParseVersion(httpRequest.Headers.IfMatch, allowZero: false, out var expectedVersion))
            {
                return SettingsEndpoints.PreconditionRequired();
            }

            var sprint = await sender.Send(
                new StartSprintCommand(sprintId, request.Goal, request.StartDate, request.EndDate, expectedVersion),
                cancellationToken);
            httpResponse.Headers.ETag = $"\"{sprint.Version}\"";
            return Results.Ok(sprint);
        })
        .WithName("StartSprint")
        .WithTags("Sprints");

        group.MapPost("/sprints/{sprintId:guid}/complete", async (
            Guid sprintId,
            CompleteSprintRequest request,
            HttpRequest httpRequest,
            HttpResponse httpResponse,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!SettingsEndpoints.TryParseVersion(httpRequest.Headers.IfMatch, allowZero: false, out var expectedVersion))
            {
                return SettingsEndpoints.PreconditionRequired();
            }

            var sprint = await sender.Send(
                new CompleteSprintCommand(sprintId, expectedVersion, request.RolloverTargetSprintId),
                cancellationToken);
            httpResponse.Headers.ETag = $"\"{sprint.Version}\"";
            return Results.Ok(sprint);
        })
        .WithName("CompleteSprint")
        .WithTags("Sprints");

        group.MapPost("/sprints/{sprintId:guid}/reopen", async (
            Guid sprintId,
            HttpRequest httpRequest,
            HttpResponse httpResponse,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!SettingsEndpoints.TryParseVersion(httpRequest.Headers.IfMatch, allowZero: false, out var expectedVersion))
            {
                return SettingsEndpoints.PreconditionRequired();
            }

            var sprint = await sender.Send(new ReopenSprintCommand(sprintId, expectedVersion), cancellationToken);
            httpResponse.Headers.ETag = $"\"{sprint.Version}\"";
            return Results.Ok(sprint);
        })
        .WithName("ReopenSprint")
        .WithTags("Sprints");

        group.MapGet("/sprints/{sprintId:guid}/report", async (
            Guid sprintId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new SprintReportQuery(sprintId), cancellationToken)))
        .WithName("GetSprintReport")
        .WithTags("Sprints");

        group.MapGet("/sprints/{sprintId:guid}/reports/cumulative-flow", async (
            Guid sprintId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new CumulativeFlowDiagramQuery(sprintId), cancellationToken)))
        .WithName("GetSprintCumulativeFlowDiagram")
        .WithTags("Sprints");

        group.MapGet("/sprints/{sprintId:guid}/reports/cycle-time", async (
            Guid sprintId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new CycleTimeReportQuery(sprintId), cancellationToken)))
        .WithName("GetSprintCycleTimeReport")
        .WithTags("Sprints");

        group.MapGet("/sprints/{sprintId:guid}/reports/control-chart", async (
            Guid sprintId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ControlChartQuery(sprintId), cancellationToken)))
        .WithName("GetSprintControlChart")
        .WithTags("Sprints");

        group.MapPut("/work-items/{workItemId:guid}/sprint", async (
            Guid workItemId,
            AssignWorkItemToSprintRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(
                new AssignWorkItemToSprintCommand(workItemId, request.SprintId), cancellationToken)))
        .WithName("AssignWorkItemToSprint")
        .WithTags("Sprints");

        group.MapDelete("/work-items/{workItemId:guid}/sprint", async (
            Guid workItemId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new RemoveWorkItemFromSprintCommand(workItemId), cancellationToken)))
        .WithName("RemoveWorkItemFromSprint")
        .WithTags("Sprints");

        return group;
    }

    public sealed record CreateSprintRequest(string Name);

    public sealed record UpdateSprintRequest(string Name, string? Goal, DateOnly? StartDate, DateOnly? EndDate);

    public sealed record StartSprintRequest(string? Goal, DateOnly? StartDate, DateOnly? EndDate);

    public sealed record CompleteSprintRequest(Guid? RolloverTargetSprintId);

    public sealed record AssignWorkItemToSprintRequest(Guid SprintId);
}
