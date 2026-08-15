using MediatR;
using Microsoft.Extensions.Configuration;
using Orbit.Application.Identity;

namespace Orbit.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/auth/login", async (
            LoginRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new LoginCommand(
                    request.Email,
                    request.Password,
                    request.WorkspaceId,
                    ClientContext.UserAgent(httpContext),
                    ClientContext.IpAddress(httpContext)),
                cancellationToken);
            return Results.Ok(result);
        })
        .WithName("Login")
        .WithTags("Auth")
        .AllowAnonymous()
        .RequireRateLimiting("auth");

        group.MapPost("/auth/refresh", async (
            RefreshRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new RefreshSessionCommand(
                    request.RefreshToken,
                    request.WorkspaceId,
                    ClientContext.UserAgent(httpContext),
                    ClientContext.IpAddress(httpContext)),
                cancellationToken);
            return Results.Ok(result);
        })
        .WithName("RefreshSession")
        .WithTags("Auth")
        .AllowAnonymous()
        .RequireRateLimiting("auth");

        group.MapPost("/auth/logout", async (
            LogoutRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new LogoutCommand(request.RefreshToken), cancellationToken);
            return Results.NoContent();
        })
        .WithName("Logout")
        .WithTags("Auth")
        .AllowAnonymous()
        .RequireRateLimiting("auth");

        group.MapPost("/auth/password-reset/request", async (
            PasswordResetRequestRequest request,
            IConfiguration configuration,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var frontendBaseUrl = configuration["Frontend:BaseUrl"]
                ?? throw new InvalidOperationException("Frontend:BaseUrl is required.");
            await sender.Send(new RequestPasswordResetCommand(request.Email, frontendBaseUrl), cancellationToken);
            return Results.Accepted();
        })
        .WithName("RequestPasswordReset")
        .WithTags("Auth")
        .AllowAnonymous()
        .RequireRateLimiting("auth");

        group.MapPost("/auth/password-reset/confirm", async (
            PasswordResetConfirmRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(
                new ConfirmPasswordResetCommand(request.Token, request.NewPassword),
                cancellationToken);
            return Results.NoContent();
        })
        .WithName("ConfirmPasswordReset")
        .WithTags("Auth")
        .AllowAnonymous()
        .RequireRateLimiting("auth");

        group.MapPost("/auth/service-token", async (
            ServiceTokenRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var token = await sender.Send(
                new IssueServiceAccountTokenCommand(request.ClientId, request.ClientSecret),
                cancellationToken);
            return Results.Ok(token);
        })
        .WithName("IssueServiceAccountToken")
        .WithTags("Auth")
        .AllowAnonymous()
        .RequireRateLimiting("auth");

        return group;
    }

    public sealed record LoginRequest(string Email, string Password, Guid? WorkspaceId);

    public sealed record RefreshRequest(string RefreshToken, Guid? WorkspaceId);

    public sealed record LogoutRequest(string RefreshToken);

    public sealed record PasswordResetRequestRequest(string Email);

    public sealed record PasswordResetConfirmRequest(string Token, string NewPassword);

    public sealed record ServiceTokenRequest(string ClientId, string ClientSecret);
}

internal static class ClientContext
{
    public static string? UserAgent(HttpContext context)
    {
        var value = context.Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static string? IpAddress(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString();
}
