using Orbit.Application.Abstractions;

namespace Orbit.Application.Common;

internal static class PrincipalGuards
{
    public static Guid RequireUser(ICurrentPrincipal principal) =>
        principal.UserId ?? throw new AccessDeniedException("A linked user account is required.");
}
