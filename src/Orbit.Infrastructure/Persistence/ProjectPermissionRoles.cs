using Orbit.Domain.Access;

namespace Orbit.Infrastructure.Persistence;

internal static class ProjectPermissionRoles
{
    public static ProjectRole[] For(ProjectPermission permission) => permission switch
    {
        ProjectPermission.View => [ProjectRole.Administrator, ProjectRole.Member, ProjectRole.Viewer],
        ProjectPermission.CreateWorkItem or ProjectPermission.TransitionWorkItem =>
            [ProjectRole.Administrator, ProjectRole.Member],
        ProjectPermission.Administer => [ProjectRole.Administrator],
        _ => [ProjectRole.Administrator],
    };
}
