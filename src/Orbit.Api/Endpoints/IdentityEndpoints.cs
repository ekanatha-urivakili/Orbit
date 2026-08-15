using MediatR;
using Orbit.Application.Identity;
using Orbit.Application.Settings;
using Orbit.Domain.Settings;

namespace Orbit.Api.Endpoints;

public static class IdentityEndpoints
{
    public static RouteGroupBuilder MapIdentityEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/me", async (
            HttpResponse response,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var profile = await sender.Send(new GetProfileQuery(), cancellationToken);
            response.Headers.ETag = $"\"{profile.Version}\"";
            return Results.Ok(profile);
        })
        .WithName("GetCurrentProfile")
        .WithTags("Identity");

        group.MapGet("/me/workspaces", async (
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListAccountWorkspacesQuery(), cancellationToken)))
        .WithName("ListAccountWorkspaces")
        .WithTags("Identity");

        group.MapPatch("/me/profile", async (
            UpdateProfileRequest request,
            HttpRequest httpRequest,
            HttpResponse response,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!SettingsEndpoints.TryParseVersion(httpRequest.Headers.IfMatch, allowZero: false, out var version))
            {
                return SettingsEndpoints.PreconditionRequired();
            }

            var profile = await sender.Send(
                new UpdateProfileCommand(request.DisplayName, request.AvatarUrl, version),
                cancellationToken);
            response.Headers.ETag = $"\"{profile.Version}\"";
            return Results.Ok(profile);
        })
        .WithName("UpdateCurrentProfile")
        .WithTags("Identity");

        group.MapPatch("/me/preferences", async (
            UpdateUserPreferenceRequest request,
            HttpRequest httpRequest,
            HttpResponse response,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!SettingsEndpoints.TryParseVersion(httpRequest.Headers.IfMatch, allowZero: true, out var version))
            {
                return SettingsEndpoints.PreconditionRequired();
            }

            var profile = await sender.Send(
                new UpdateUserPreferenceCommand(
                    request.Locale,
                    request.TimeZone,
                    request.Theme,
                    request.Density,
                    request.ReduceMotion,
                    request.HighContrast,
                    version),
                cancellationToken);
            response.Headers.ETag = $"\"{profile.PreferenceVersion}\"";
            return Results.Ok(profile);
        })
        .WithName("UpdateCurrentPreferences")
        .WithTags("Identity");

        group.MapGet("/me/notification-preferences", async (
            HttpResponse response,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var preference = await sender.Send(new GetNotificationPreferenceQuery(), cancellationToken);
            response.Headers.ETag = $"\"{preference.Version}\"";
            return Results.Ok(preference);
        })
        .WithName("GetNotificationPreferences")
        .WithTags("Identity");

        group.MapPatch("/me/notification-preferences", async (
            UpdateNotificationPreferenceRequest request,
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
                new UpdateNotificationPreferenceCommand(
                    request.InAppEnabled,
                    request.EmailEnabled,
                    request.DigestCadence,
                    request.QuietHoursStart,
                    request.QuietHoursEnd,
                    request.SelfNotify,
                    version),
                cancellationToken);
            response.Headers.ETag = $"\"{preference.Version}\"";
            return Results.Ok(preference);
        })
        .WithName("UpdateNotificationPreferences")
        .WithTags("Identity");

        group.MapGet("/me/sessions", async (
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListSessionsQuery(), cancellationToken)))
        .WithName("ListSessions")
        .WithTags("Identity");

        group.MapDelete("/me/sessions/{sessionId:guid}", async (
            Guid sessionId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new RevokeSessionCommand(sessionId), cancellationToken);
            return Results.NoContent();
        })
        .WithName("RevokeSession")
        .WithTags("Identity");

        group.MapDelete("/me/sessions", async (
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var revoked = await sender.Send(new RevokeOtherSessionsCommand(), cancellationToken);
            return Results.Ok(new { revokedCount = revoked });
        })
        .WithName("RevokeOtherSessions")
        .WithTags("Identity");

        group.MapGet("/me/external-identities", async (
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListLinkedIdentitiesQuery(), cancellationToken)))
        .WithName("ListLinkedIdentities")
        .WithTags("Identity");

        group.MapPost("/me/external-identities", async (
            LinkExternalIdentityRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var identity = await sender.Send(
                new LinkExternalIdentityCommand(request.IdentityToken), cancellationToken);
            return Results.Created($"/api/v1/me/external-identities/{identity.Id}", identity);
        })
        .WithName("LinkExternalIdentity")
        .WithTags("Identity");

        group.MapDelete("/me/external-identities/{identityId:guid}", async (
            Guid identityId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UnlinkExternalIdentityCommand(identityId), cancellationToken);
            return Results.NoContent();
        })
        .WithName("UnlinkExternalIdentity")
        .WithTags("Identity");

        return group;
    }

    public sealed record UpdateProfileRequest(string DisplayName, string? AvatarUrl);

    public sealed record UpdateUserPreferenceRequest(
        string Locale,
        string TimeZone,
        ThemePreference Theme,
        DensityPreference Density,
        bool ReduceMotion,
        bool HighContrast);

    public sealed record UpdateNotificationPreferenceRequest(
        bool InAppEnabled,
        bool EmailEnabled,
        DigestCadence DigestCadence,
        TimeOnly? QuietHoursStart,
        TimeOnly? QuietHoursEnd,
        bool SelfNotify);

    public sealed record LinkExternalIdentityRequest(string IdentityToken);
}
