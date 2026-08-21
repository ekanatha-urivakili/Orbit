using Orbit.Application.Abstractions;
using Orbit.Domain.Access;

namespace Orbit.Infrastructure.Authorization;

internal sealed class TenantAuthorization(ICurrentPrincipal principal) : ITenantAuthorization
{
    public bool CanCreateProject() =>
        principal.IsDevelopmentBypass
        || principal.TenantRole is TenantRole.Owner or TenantRole.Administrator;

    public bool CanCreateMembership(TenantRole role) =>
        principal.IsDevelopmentBypass
        || principal.TenantRole == TenantRole.Owner
        || principal.TenantRole == TenantRole.Administrator && role == TenantRole.Member;

    public bool CanManageTeams() =>
        principal.IsDevelopmentBypass
        || principal.TenantRole is TenantRole.Owner or TenantRole.Administrator;

    public bool CanManageRoles() =>
        principal.IsDevelopmentBypass
        || principal.TenantRole is TenantRole.Owner or TenantRole.Administrator;
}
