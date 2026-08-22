using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Access;

namespace Orbit.Infrastructure.Persistence;

/// <summary>
/// Enumerates every user who can see a project, mirroring <see cref="ProjectAccessQuery"/>'s
/// per-principal policy but inverted: given a project, who has access, rather than given a
/// principal, which projects. Used to notify everyone with board/backlog access on sprint close
/// (§13.5), where the recipient set isn't tied to any single caller's membership.
/// </summary>
internal sealed class ProjectAccessRepository(OrbitDbContext dbContext) : IProjectAccessRepository
{
    public async Task<IReadOnlyList<Guid>> ListUserIdsWithPermissionAsync(
        Guid tenantId, Guid projectId, ProjectPermission permission, CancellationToken cancellationToken)
    {
        var tenantWide = dbContext.TenantMemberships.Where(membership =>
            membership.TenantId == tenantId
            && membership.UserId != null
            && membership.IsActive
            && membership.Tier != MembershipTier.Guest
            && (membership.Role == TenantRole.Owner || membership.Role == TenantRole.Administrator));

        var explicitAssignment = dbContext.TenantMemberships.Where(membership =>
            membership.TenantId == tenantId
            && membership.UserId != null
            && membership.IsActive
            && dbContext.ProjectRoleAssignments.Any(assignment =>
                assignment.TenantId == tenantId
                && assignment.ProjectId == projectId
                && assignment.MembershipId == membership.Id
                && dbContext.RolePermissions.Any(rolePermission =>
                    rolePermission.RoleId == assignment.RoleId && rolePermission.Permission == permission)));

        var groupAssignment = dbContext.TenantMemberships.Where(membership =>
            membership.TenantId == tenantId
            && membership.UserId != null
            && membership.IsActive
            && dbContext.ProjectGroupRoleAssignments.Any(assignment =>
                assignment.TenantId == tenantId
                && assignment.ProjectId == projectId
                && dbContext.RolePermissions.Any(rolePermission =>
                    rolePermission.RoleId == assignment.RoleId && rolePermission.Permission == permission)
                && dbContext.GroupMemberships.Any(groupMembership =>
                    groupMembership.TenantId == tenantId
                    && groupMembership.GroupId == assignment.GroupId
                    && groupMembership.MembershipId == membership.Id)));

        return await tenantWide.Concat(explicitAssignment).Concat(groupAssignment)
            .Select(membership => membership.UserId!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }
}
