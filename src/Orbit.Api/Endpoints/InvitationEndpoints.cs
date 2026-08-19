using MediatR;
using Orbit.Application.Access;
using Orbit.Domain.Access;

namespace Orbit.Api.Endpoints;

public static class InvitationEndpoints
{
    public static RouteGroupBuilder MapInvitationAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/invitations", async (
            string? email,
            WorkspaceInvitationStatus? status,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListWorkspaceInvitationsQuery(email, status), cancellationToken)))
            .WithName("ListWorkspaceInvitations")
            .WithTags("Invitations");

        group.MapPost("/invitations", async (
            CreateWorkspaceInvitationRequest request,
            IConfiguration configuration,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var frontendBaseUrl = configuration["Frontend:BaseUrl"]
                ?? throw new InvalidOperationException("Frontend:BaseUrl is required.");
            var invitation = await sender.Send(
                new CreateWorkspaceInvitationCommand(
                    request.Email,
                    request.Role,
                    request.TeamId,
                    frontendBaseUrl,
                    request.Tier),
                cancellationToken);
            return Results.Created($"/api/v1/invitations/{invitation.Id}", invitation);
        })
        .WithName("CreateWorkspaceInvitation")
        .WithTags("Invitations");

        group.MapDelete("/invitations/{invitationId:guid}", async (
            Guid invitationId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new RevokeWorkspaceInvitationCommand(invitationId), cancellationToken);
            return Results.NoContent();
        })
        .WithName("RevokeWorkspaceInvitation")
        .WithTags("Invitations");

        return group;
    }

    public static RouteGroupBuilder MapInvitationAcceptanceEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/workspaces/{tenantId:guid}/invitations/accept", async (
            Guid tenantId,
            AcceptWorkspaceInvitationRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            _ = tenantId;
            var membership = await sender.Send(
                new AcceptWorkspaceInvitationCommand(request.Token, request.DisplayName, request.Password),
                cancellationToken);
            return Results.Ok(membership);
        })
        .WithName("AcceptWorkspaceInvitation")
        .WithTags("Invitations")
        .AllowAnonymous()
        .RequireRateLimiting("auth");

        group.MapPost("/workspaces/{tenantId:guid}/invitations/accept-external", async (
            Guid tenantId,
            AcceptWorkspaceInvitationWithExternalIdentityRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            _ = tenantId;
            var membership = await sender.Send(
                new AcceptWorkspaceInvitationWithExternalIdentityCommand(
                    request.Token, request.ExternalIdToken, request.DisplayName),
                cancellationToken);
            return Results.Ok(membership);
        })
        .WithName("AcceptWorkspaceInvitationWithExternalIdentity")
        .WithTags("Invitations")
        .AllowAnonymous()
        .RequireRateLimiting("auth");

        return group;
    }

    public sealed record CreateWorkspaceInvitationRequest(
        string Email,
        TenantRole Role,
        Guid? TeamId,
        MembershipTier Tier = MembershipTier.Standard);

    public sealed record AcceptWorkspaceInvitationRequest(string Token, string DisplayName, string Password);

    public sealed record AcceptWorkspaceInvitationWithExternalIdentityRequest(
        string Token,
        string ExternalIdToken,
        string DisplayName);
}
