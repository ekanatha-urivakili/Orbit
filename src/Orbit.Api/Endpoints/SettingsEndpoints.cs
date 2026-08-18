using MediatR;
using Orbit.Application.Settings;
using Orbit.Domain.Choices;

namespace Orbit.Api.Endpoints;

public static class SettingsEndpoints
{
    public static RouteGroupBuilder MapSettingsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/workspaces/current/settings", async (
            HttpResponse response,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var setting = await sender.Send(new GetWorkspaceSettingQuery(), cancellationToken);
            response.Headers.ETag = $"\"{setting.Version}\"";
            return Results.Ok(setting);
        })
        .WithName("GetWorkspaceSettings")
        .WithTags("Settings");

        group.MapPatch("/workspaces/current/settings", async (
            UpdateWorkspaceSettingRequest request,
            HttpRequest httpRequest,
            HttpResponse response,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseVersion(httpRequest.Headers.IfMatch, allowZero: true, out var version))
            {
                return PreconditionRequired();
            }

            var setting = await sender.Send(
                new UpdateWorkspaceSettingCommand(
                    request.Description,
                    request.DefaultLocale,
                    request.DefaultTimeZone,
                    request.AllowMemberProjectCreation,
                    version),
                cancellationToken);
            response.Headers.ETag = $"\"{setting.Version}\"";
            return Results.Ok(setting);
        })
        .WithName("UpdateWorkspaceSettings")
        .WithTags("Settings");

        group.MapPost("/workspaces/current/settings/logo/presign", async (
            PresignWorkspaceLogoUploadRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new PresignWorkspaceLogoUploadCommand(request.FileName, request.ContentType, request.SizeBytes),
                cancellationToken);
            return Results.Ok(result);
        })
        .WithName("PresignWorkspaceLogoUpload")
        .WithTags("Settings");

        group.MapPut("/workspaces/current/settings/logo", async (
            ConfirmWorkspaceLogoUploadRequest request,
            HttpRequest httpRequest,
            HttpResponse response,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseVersion(httpRequest.Headers.IfMatch, allowZero: true, out var version))
            {
                return PreconditionRequired();
            }

            var setting = await sender.Send(
                new ConfirmWorkspaceLogoUploadCommand(request.ObjectKey, version),
                cancellationToken);
            response.Headers.ETag = $"\"{setting.Version}\"";
            return Results.Ok(setting);
        })
        .WithName("ConfirmWorkspaceLogoUpload")
        .WithTags("Settings");

        group.MapGet("/workspaces/current/typography-settings", async (
            HttpResponse response,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var setting = await sender.Send(new GetTypographySettingQuery(), cancellationToken);
            response.Headers.ETag = $"\"{setting.Version}\"";
            return Results.Ok(setting);
        })
        .WithName("GetWorkspaceTypographySettings")
        .WithTags("Settings");

        group.MapPatch("/workspaces/current/typography-settings", async (
            UpdateTypographySettingRequest request,
            HttpRequest httpRequest,
            HttpResponse response,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseVersion(httpRequest.Headers.IfMatch, allowZero: true, out var version))
            {
                return PreconditionRequired();
            }

            var setting = await sender.Send(
                new UpdateTypographySettingCommand(
                    request.LeftFontFamily,
                    request.LeftFontColor,
                    request.LeftFontSizePx,
                    request.MiddleFontFamily,
                    request.MiddleFontColor,
                    request.MiddleFontSizePx,
                    request.RightFontFamily,
                    request.RightFontColor,
                    request.RightFontSizePx,
                    request.ControlHeightPx,
                    request.ControlFontSizePx,
                    version),
                cancellationToken);
            response.Headers.ETag = $"\"{setting.Version}\"";
            return Results.Ok(setting);
        })
        .WithName("UpdateWorkspaceTypographySettings")
        .WithTags("Settings");

        group.MapGet("/projects/{projectId:guid}/settings", async (
            Guid projectId,
            HttpResponse response,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var setting = await sender.Send(new GetProjectSettingQuery(projectId), cancellationToken);
            response.Headers.ETag = $"\"{setting.Version}\"";
            return Results.Ok(setting);
        })
        .WithName("GetProjectSettings")
        .WithTags("Settings");

        group.MapPatch("/projects/{projectId:guid}/settings", async (
            Guid projectId,
            UpdateProjectSettingRequest request,
            HttpRequest httpRequest,
            HttpResponse response,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseVersion(httpRequest.Headers.IfMatch, allowZero: true, out var version))
            {
                return PreconditionRequired();
            }

            var setting = await sender.Send(
                new UpdateProjectSettingCommand(
                    projectId,
                    request.DefaultWorkItemType,
                    request.DefaultPriority,
                    request.EnableReleases,
                    request.EnableTimeTracking,
                    request.RepositoryUrl,
                    version),
                cancellationToken);
            response.Headers.ETag = $"\"{setting.Version}\"";
            return Results.Ok(setting);
        })
        .WithName("UpdateProjectSettings")
        .WithTags("Settings");

        return group;
    }

    internal static bool TryParseVersion(string? header, bool allowZero, out long version)
    {
        version = -1;
        return !string.IsNullOrWhiteSpace(header)
            && long.TryParse(header.Trim().Trim('"'), out version)
            && (allowZero ? version >= 0 : version > 0);
    }

    internal static IResult PreconditionRequired() => Results.Problem(
        statusCode: StatusCodes.Status428PreconditionRequired,
        type: "/problems/if-match-required",
        title: "A numeric If-Match header is required.");

    public sealed record UpdateWorkspaceSettingRequest(
        string? Description,
        string DefaultLocale,
        string DefaultTimeZone,
        bool AllowMemberProjectCreation);

    public sealed record PresignWorkspaceLogoUploadRequest(string FileName, string ContentType, long SizeBytes);

    public sealed record ConfirmWorkspaceLogoUploadRequest(string ObjectKey);

    public sealed record UpdateProjectSettingRequest(
        WorkItemType DefaultWorkItemType,
        Priority DefaultPriority,
        bool EnableReleases,
        bool EnableTimeTracking,
        string? RepositoryUrl);

    public sealed record UpdateTypographySettingRequest(
        string LeftFontFamily,
        string LeftFontColor,
        int LeftFontSizePx,
        string MiddleFontFamily,
        string MiddleFontColor,
        int MiddleFontSizePx,
        string RightFontFamily,
        string RightFontColor,
        int RightFontSizePx,
        int ControlHeightPx,
        int ControlFontSizePx);
}
