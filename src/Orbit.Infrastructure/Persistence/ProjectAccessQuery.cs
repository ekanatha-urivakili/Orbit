using Orbit.Application.Abstractions;
using Orbit.Domain.Access;
using Orbit.Domain.Projects;

namespace Orbit.Infrastructure.Persistence;

internal static class ProjectAccessQuery
{
    public static IQueryable<Project> PermittedProjects(
        OrbitDbContext dbContext,
        ICurrentPrincipal principal,
        Guid tenantId,
        ProjectPermission permission)
    {
        var tenantWideAccess = principal.IsDevelopmentBypass
            || principal.TenantRole is TenantRole.Owner or TenantRole.Administrator;
        var membershipId = principal.MembershipId;
        var allowedRoles = ProjectPermissionRoles.For(permission);

        return dbContext.Projects.Where(project =>
            project.TenantId == tenantId
            && (tenantWideAccess
                || dbContext.ProjectRoleAssignments.Any(assignment =>
                    assignment.TenantId == tenantId
                    && assignment.ProjectId == project.Id
                    && assignment.MembershipId == membershipId
                    && allowedRoles.Contains(assignment.Role))
                || dbContext.ProjectGroupRoleAssignments.Any(assignment =>
                    assignment.TenantId == tenantId
                    && assignment.ProjectId == project.Id
                    && allowedRoles.Contains(assignment.Role)
                    && dbContext.GroupMemberships.Any(groupMembership =>
                        groupMembership.TenantId == tenantId
                        && groupMembership.GroupId == assignment.GroupId
                        && groupMembership.MembershipId == membershipId))));
    }
}
