using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;

namespace Orbit.Application.Access;

public sealed record AssignProjectRoleCommand(
    Guid ProjectId,
    Guid MembershipId,
    Guid RoleId) : ICommand<ProjectRoleAssignmentDto>;

public sealed record ProjectRoleAssignmentDto(
    Guid Id,
    Guid ProjectId,
    Guid MembershipId,
    Guid RoleId,
    DateTimeOffset CreatedAt)
{
    public static ProjectRoleAssignmentDto From(ProjectRoleAssignment assignment) =>
        new(
            assignment.Id,
            assignment.ProjectId,
            assignment.MembershipId,
            assignment.RoleId,
            assignment.CreatedAt);
}

public sealed class AssignProjectRoleValidator : AbstractValidator<AssignProjectRoleCommand>
{
    public AssignProjectRoleValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.MembershipId).NotEmpty();
        RuleFor(command => command.RoleId).NotEmpty();
    }
}

public sealed class AssignProjectRoleHandler(
    ITenantContext tenantContext,
    IProjectRepository projects,
    ITenantMembershipRepository memberships,
    IProjectRoleRepository projectRoles,
    IRoleRepository roles,
    ISettingsRepository settings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<AssignProjectRoleCommand, ProjectRoleAssignmentDto>
{
    public async Task<ProjectRoleAssignmentDto> Handle(
        AssignProjectRoleCommand request,
        CancellationToken cancellationToken)
    {
        _ = await projects.GetAsync(
            tenantContext.TenantId,
            request.ProjectId,
            ProjectPermission.Administer,
            cancellationToken)
            ?? throw new NotFoundException("Project was not found.");
        _ = await memberships.GetActiveAsync(
            tenantContext.TenantId,
            request.MembershipId,
            cancellationToken)
            ?? throw new NotFoundException("Tenant membership was not found.");
        _ = await roles.GetAsync(tenantContext.TenantId, request.RoleId, cancellationToken)
            ?? throw new NotFoundException("Role was not found.");

        var assignment = await projectRoles.GetAsync(
            tenantContext.TenantId,
            request.ProjectId,
            request.MembershipId,
            cancellationToken);
        if (assignment is null)
        {
            assignment = ProjectRoleAssignment.Create(
                tenantContext.TenantId,
                request.ProjectId,
                request.MembershipId,
                request.RoleId,
                timeProvider.GetUtcNow());
            await projectRoles.AddAsync(assignment, cancellationToken);
        }
        else
        {
            assignment.ChangeRole(request.RoleId);
        }

        var workspace = await settings.GetWorkspaceAsync(tenantContext.TenantId, cancellationToken)
            ?? throw new NotFoundException("Workspace was not found.");
        workspace.IncrementAuthorizationEpoch();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ProjectRoleAssignmentDto.From(assignment);
    }
}
