using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;

namespace Orbit.Application.Access;

public sealed record AssignGroupProjectRoleCommand(
    Guid ProjectId,
    Guid GroupId,
    ProjectRole Role) : ICommand<ProjectGroupRoleAssignmentDto>;

public sealed record ProjectGroupRoleAssignmentDto(
    Guid Id,
    Guid ProjectId,
    Guid GroupId,
    ProjectRole Role,
    DateTimeOffset CreatedAt)
{
    public static ProjectGroupRoleAssignmentDto From(ProjectGroupRoleAssignment assignment) =>
        new(
            assignment.Id,
            assignment.ProjectId,
            assignment.GroupId,
            assignment.Role,
            assignment.CreatedAt);
}

public sealed class AssignGroupProjectRoleValidator : AbstractValidator<AssignGroupProjectRoleCommand>
{
    public AssignGroupProjectRoleValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.GroupId).NotEmpty();
        RuleFor(command => command.Role).IsInEnum();
    }
}

public sealed class AssignGroupProjectRoleHandler(
    ITenantContext tenantContext,
    IProjectRepository projects,
    IDirectoryGroupRepository groups,
    IProjectGroupRoleRepository projectGroupRoles,
    ISettingsRepository settings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<AssignGroupProjectRoleCommand, ProjectGroupRoleAssignmentDto>
{
    public async Task<ProjectGroupRoleAssignmentDto> Handle(
        AssignGroupProjectRoleCommand request,
        CancellationToken cancellationToken)
    {
        _ = await projects.GetAsync(
            tenantContext.TenantId,
            request.ProjectId,
            ProjectPermission.Administer,
            cancellationToken)
            ?? throw new NotFoundException("Project was not found.");
        _ = await groups.GetAsync(
            tenantContext.TenantId,
            request.GroupId,
            cancellationToken)
            ?? throw new NotFoundException("Group was not found.");

        var assignment = await projectGroupRoles.GetAsync(
            tenantContext.TenantId,
            request.ProjectId,
            request.GroupId,
            cancellationToken);
        if (assignment is null)
        {
            assignment = ProjectGroupRoleAssignment.Create(
                tenantContext.TenantId,
                request.ProjectId,
                request.GroupId,
                request.Role,
                timeProvider.GetUtcNow());
            await projectGroupRoles.AddAsync(assignment, cancellationToken);
        }
        else
        {
            assignment.ChangeRole(request.Role);
        }

        var workspace = await settings.GetWorkspaceAsync(tenantContext.TenantId, cancellationToken)
            ?? throw new NotFoundException("Workspace was not found.");
        workspace.IncrementAuthorizationEpoch();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ProjectGroupRoleAssignmentDto.From(assignment);
    }
}
