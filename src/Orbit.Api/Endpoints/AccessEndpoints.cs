using MediatR;
using Orbit.Application.Access;
using Orbit.Application.Identity;
using Orbit.Domain.Access;

namespace Orbit.Api.Endpoints;

public static class AccessEndpoints
{
    public static RouteGroupBuilder MapAccessEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/service-accounts", async (
            CreateServiceAccountRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var credential = await sender.Send(new CreateServiceAccountCommand(request.Role), cancellationToken);
            return Results.Created($"/api/v1/memberships/{credential.MembershipId}", credential);
        })
        .WithName("CreateServiceAccount")
        .WithTags("Access");

        group.MapPost("/service-accounts/{membershipId:guid}/rotate", async (
            Guid membershipId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new RotateServiceAccountCredentialCommand(membershipId), cancellationToken)))
            .WithName("RotateServiceAccountCredential")
            .WithTags("Access");

        group.MapGet("/memberships", async (
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListTenantMembershipsQuery(), cancellationToken)))
            .WithName("ListTenantMemberships")
            .WithTags("Access");

        group.MapGet("/projects/{projectId:guid}/roles", async (
            Guid projectId,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListProjectRoleAssignmentsQuery(projectId), cancellationToken)))
            .WithName("ListProjectRoleAssignments")
            .WithTags("Access");

        group.MapPost("/memberships", async (
            CreateTenantMembershipRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var membership = await sender.Send(
                new CreateTenantMembershipCommand(
                    request.Issuer,
                    request.Subject,
                    request.PrincipalType,
                    request.Role),
                cancellationToken);
            return Results.Created($"/api/v1/memberships/{membership.Id}", membership);
        })
        .WithName("CreateTenantMembership")
        .WithTags("Access");

        group.MapPut("/projects/{projectId:guid}/roles/{membershipId:guid}", async (
            Guid projectId,
            Guid membershipId,
            AssignProjectRoleRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var assignment = await sender.Send(
                new AssignProjectRoleCommand(projectId, membershipId, request.Role),
                cancellationToken);
            return Results.Ok(assignment);
        })
        .WithName("AssignProjectRole")
        .WithTags("Access");

        group.MapPut("/projects/{projectId:guid}/group-roles/{groupId:guid}", async (
            Guid projectId,
            Guid groupId,
            AssignGroupProjectRoleRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var assignment = await sender.Send(
                new AssignGroupProjectRoleCommand(projectId, groupId, request.Role),
                cancellationToken);
            return Results.Ok(assignment);
        })
        .WithName("AssignGroupProjectRole")
        .WithTags("Access");

        group.MapPut("/memberships/{membershipId:guid}/role", async (
            Guid membershipId,
            ChangeTenantMembershipRoleRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(
                new ChangeTenantMembershipRoleCommand(membershipId, request.Role),
                cancellationToken)))
            .WithName("ChangeTenantMembershipRole")
            .WithTags("Access");

        group.MapDelete("/memberships/{membershipId:guid}", async (
            Guid membershipId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeactivateTenantMembershipCommand(membershipId), cancellationToken);
            return Results.NoContent();
        })
        .WithName("DeactivateTenantMembership")
        .WithTags("Access");

        return group;
    }

    public sealed record CreateTenantMembershipRequest(
        string Issuer,
        string Subject,
        PrincipalType PrincipalType,
        TenantRole Role);

    public sealed record AssignProjectRoleRequest(ProjectRole Role);

    public sealed record AssignGroupProjectRoleRequest(ProjectRole Role);

    public sealed record ChangeTenantMembershipRoleRequest(TenantRole Role);

    public sealed record CreateServiceAccountRequest(TenantRole Role);
}
