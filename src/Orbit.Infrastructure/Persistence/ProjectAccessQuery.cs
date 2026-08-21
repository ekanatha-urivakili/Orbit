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
        // A guest never gets tenant-wide access, even if somehow granted Owner/Administrator (the
        // domain model already rejects that combination at membership-creation time - this is
        // defense in depth, not the only enforcement point).
        var tenantWideAccess = principal.MembershipTier != MembershipTier.Guest
            && (principal.IsDevelopmentBypass || principal.TenantRole is TenantRole.Owner or TenantRole.Administrator);
        var membershipId = principal.MembershipId;

        return dbContext.Projects.Where(project =>
            project.TenantId == tenantId
            && (tenantWideAccess
                || dbContext.ProjectRoleAssignments.Any(assignment =>
                    assignment.TenantId == tenantId
                    && assignment.ProjectId == project.Id
                    && assignment.MembershipId == membershipId
                    && dbContext.RolePermissions.Any(rolePermission =>
                        rolePermission.RoleId == assignment.RoleId && rolePermission.Permission == permission))
                || dbContext.ProjectGroupRoleAssignments.Any(assignment =>
                    assignment.TenantId == tenantId
                    && assignment.ProjectId == project.Id
                    && dbContext.RolePermissions.Any(rolePermission =>
                        rolePermission.RoleId == assignment.RoleId && rolePermission.Permission == permission)
                    && dbContext.GroupMemberships.Any(groupMembership =>
                        groupMembership.TenantId == tenantId
                        && groupMembership.GroupId == assignment.GroupId
                        && groupMembership.MembershipId == membershipId))));
    }
}
