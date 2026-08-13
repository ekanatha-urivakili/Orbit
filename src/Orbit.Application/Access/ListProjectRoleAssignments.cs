using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;

namespace Orbit.Application.Access;

public sealed record ListProjectRoleAssignmentsQuery(Guid ProjectId) : IQuery<IReadOnlyList<ProjectRoleAssignmentDto>>;

public sealed class ListProjectRoleAssignmentsValidator : AbstractValidator<ListProjectRoleAssignmentsQuery>
{
    public ListProjectRoleAssignmentsValidator()
    {
        RuleFor(query => query.ProjectId).NotEmpty();
    }
}

public sealed class ListProjectRoleAssignmentsHandler(
    ITenantContext tenantContext,
    IProjectRepository projects,
    IProjectRoleRepository projectRoles)
    : IRequestHandler<ListProjectRoleAssignmentsQuery, IReadOnlyList<ProjectRoleAssignmentDto>>
{
    public async Task<IReadOnlyList<ProjectRoleAssignmentDto>> Handle(
        ListProjectRoleAssignmentsQuery request,
        CancellationToken cancellationToken)
    {
        _ = await projects.GetAsync(
            tenantContext.TenantId,
            request.ProjectId,
            ProjectPermission.Administer,
            cancellationToken)
            ?? throw new NotFoundException("Project was not found.");

        var result = await projectRoles.ListByProjectAsync(tenantContext.TenantId, request.ProjectId, cancellationToken);
        return result.Select(ProjectRoleAssignmentDto.From).ToArray();
    }
}
