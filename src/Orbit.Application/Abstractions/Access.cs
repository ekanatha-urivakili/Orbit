using Orbit.Domain.Access;

namespace Orbit.Application.Abstractions;

public interface IProjectAccessRepository
{
    /// <summary>
    /// Distinct ids of every user who currently holds <paramref name="permission"/> on the
    /// project - via the tenant-wide Owner/Administrator shortcut, an explicit
    /// <see cref="ProjectRoleAssignment"/>, or a <see cref="ProjectGroupRoleAssignment"/> through
    /// directory-group membership. The enumeration-shape inverse of
    /// <c>ProjectAccessQuery.PermittedProjects</c> (which project) - "which users".
    /// </summary>
    Task<IReadOnlyList<Guid>> ListUserIdsWithPermissionAsync(
        Guid tenantId,
        Guid projectId,
        ProjectPermission permission,
        CancellationToken cancellationToken);
}
