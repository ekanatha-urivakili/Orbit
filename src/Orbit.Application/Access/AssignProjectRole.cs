using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;

namespace Orbit.Application.Access;

public sealed record AssignProjectRoleCommand(
    Guid ProjectId,
    Guid MembershipId,
    ProjectRole Role) : ICommand<ProjectRoleAssignmentDto>;

public sealed record ProjectRoleAssignmentDto(
    Guid Id,
    Guid ProjectId,
    Guid MembershipId,
    ProjectRole Role,
    DateTimeOffset CreatedAt)
{
    public static ProjectRoleAssignmentDto From(ProjectRoleAssignment assignment) =>
        new(
            assignment.Id,
            assignment.ProjectId,
            assignment.MembershipId,
            assignment.Role,
            assignment.CreatedAt);
}

public sealed class AssignProjectRoleValidator : AbstractValidator<AssignProjectRoleCommand>
{
    public AssignProjectRoleValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.MembershipId).NotEmpty();
        RuleFor(command => command.Role).IsInEnum();
    }
}

public sealed class AssignProjectRoleHandler(
    ITenantContext tenantContext,
    IProjectRepository projects,
    ITenantMembershipRepository memberships,
    IProjectRoleRepository projectRoles,
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
                request.Role,
                timeProvider.GetUtcNow());
            await projectRoles.AddAsync(assignment, cancellationToken);
        }
        else
        {
            assignment.ChangeRole(request.Role);
        }

        var workspace = await settings.GetWorkspaceAsync(tenantContext.TenantId, cancellationToken)
            ?? throw new NotFoundException("Workspace was not found.");
        workspace.IncrementAuthorizationEpoch();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ProjectRoleAssignmentDto.From(assignment);
    }
}
