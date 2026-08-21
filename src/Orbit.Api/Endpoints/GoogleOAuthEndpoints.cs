using MediatR;
using Microsoft.AspNetCore.Http;
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
            string? returnUrl,
            HttpContext httpContext,
            IConfiguration configuration,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var safeReturnUrl = ResolveSafeReturnUrl(returnUrl, httpContext, configuration);
            try
            {
                var result = await sender.Send(new GoogleOAuthStartQuery(mode, safeReturnUrl), cancellationToken);
                return Results.Redirect(result.AuthorizeUrl);
            }
            catch (Exception exception) when (exception is AuthenticationException or AccessDeniedException)
            {
                var fallback = safeReturnUrl ?? configuration["Frontend:BaseUrl"] ?? "http://localhost:5800";
                return Results.Redirect($"{fallback}/?googleAuthError={Uri.EscapeDataString(exception.Message)}");
            }
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
            var defaultFrontendBaseUrl = configuration["Frontend:BaseUrl"]
                ?? throw new InvalidOperationException("Frontend:BaseUrl is required.");

            EnsureAbsoluteHttpUri(defaultFrontendBaseUrl, "Frontend:BaseUrl");

            var targetFrontendUrl = defaultFrontendBaseUrl;

            try
            {
                var result = await sender.Send(new HandleGoogleCallbackCommand(code, state), cancellationToken);
                if (!string.IsNullOrWhiteSpace(result.ReturnUrl) && IsAllowedOrigin(result.ReturnUrl, configuration))
                {
                    targetFrontendUrl = result.ReturnUrl;
                }

                if (result.Linked)
                {
                    return Results.Redirect($"{targetFrontendUrl}/?googleLinked=true");
                }

                return Results.Redirect($"{targetFrontendUrl}/?googleAuth={Uri.EscapeDataString(result.HandoffCode!)}");
            }
            catch (Exception exception) when (exception is AuthenticationException
                or AccessDeniedException
                or NotFoundException
                or ConflictException)
            {
                return Results.Redirect($"{targetFrontendUrl}/?googleAuthError={Uri.EscapeDataString(exception.Message)}");
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

    internal static string? ResolveSafeReturnUrl(string? returnUrl, HttpContext httpContext, IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && IsAllowedOrigin(returnUrl, configuration))
        {
            return NormalizeOrigin(returnUrl);
        }

        var referer = httpContext.Request.Headers.Referer.ToString();
        if (!string.IsNullOrWhiteSpace(referer) && IsAllowedOrigin(referer, configuration))
        {
            return NormalizeOrigin(referer);
        }

        var frontendBase = configuration["Frontend:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(frontendBase) && IsAllowedOrigin(frontendBase, configuration))
        {
            return NormalizeOrigin(frontendBase);
        }

        return null;
    }

    internal static bool IsAllowedOrigin(string url, IConfiguration configuration)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var targetOrigin = $"{uri.Scheme}://{uri.Authority}".TrimEnd('/');

        var frontendBase = configuration["Frontend:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(frontendBase)
            && Uri.TryCreate(frontendBase, UriKind.Absolute, out var frontendUri)
            && string.Equals($"{frontendUri.Scheme}://{frontendUri.Authority}".TrimEnd('/'), targetOrigin, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var allowedOrigins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
        foreach (var allowed in allowedOrigins)
        {
            if (Uri.TryCreate(allowed, UriKind.Absolute, out var allowedUri)
                && string.Equals($"{allowedUri.Scheme}://{allowedUri.Authority}".TrimEnd('/'), targetOrigin, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static string NormalizeOrigin(string url)
    {
        var uri = new Uri(url, UriKind.Absolute);
        return $"{uri.Scheme}://{uri.Authority}".TrimEnd('/');
    }

    private static void EnsureAbsoluteHttpUri(string url, string propertyName)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"{propertyName} must be an absolute http or https URI.");
        }
    }

    public sealed record GoogleExchangeRequest(string Code);
}

