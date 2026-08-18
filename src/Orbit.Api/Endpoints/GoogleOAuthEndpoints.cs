using MediatR;
using Microsoft.Extensions.Configuration;
using Orbit.Application.Common;
using Orbit.Application.Identity;

namespace Orbit.Api.Endpoints;

public static class GoogleOAuthEndpoints
{
    public static RouteGroupBuilder MapGoogleOAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/auth/google/start", async (
            string mode,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GoogleOAuthStartQuery(mode), cancellationToken);
            return Results.Redirect(result.AuthorizeUrl);
        })
        .WithName("StartGoogleOAuth")
        .WithTags("Auth")
        .AllowAnonymous()
        .RequireRateLimiting("auth");

        group.MapGet("/auth/google/callback", async (
            string code,
            string state,
            IConfiguration configuration,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var frontendBaseUrl = configuration["Frontend:BaseUrl"]
                ?? throw new InvalidOperationException("Frontend:BaseUrl is required.");

            // SEC-11: Guard against open-redirect if Frontend:BaseUrl is misconfigured.
            // The value must be an absolute http/https URI.
            if (!Uri.TryCreate(frontendBaseUrl, UriKind.Absolute, out var frontendUri)
                || (frontendUri.Scheme != Uri.UriSchemeHttp && frontendUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException("Frontend:BaseUrl must be an absolute http or https URI.");
            }

            try
            {
                var result = await sender.Send(new HandleGoogleCallbackCommand(code, state), cancellationToken);
                return Results.Redirect($"{frontendBaseUrl}/?googleAuth={Uri.EscapeDataString(result.HandoffCode)}");
            }
            catch (Exception exception) when (exception is AuthenticationException
                or AccessDeniedException
                or NotFoundException
                or ConflictException)
            {
                return Results.Redirect($"{frontendBaseUrl}/?googleAuthError={Uri.EscapeDataString(exception.Message)}");
            }
        })
        .WithName("GoogleOAuthCallback")
        .WithTags("Auth")
        .AllowAnonymous()
        .RequireRateLimiting("auth");

        group.MapPost("/auth/google/exchange", async (
            GoogleExchangeRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ExchangeGoogleHandoffCommand(request.Code), cancellationToken);
            return Results.Ok(result);
        })
        .WithName("ExchangeGoogleHandoff")
        .WithTags("Auth")
        .AllowAnonymous()
        .RequireRateLimiting("auth");

        return group;
    }

    public sealed record GoogleExchangeRequest(string Code);
}
