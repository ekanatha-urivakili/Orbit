using Orbit.Application.Abstractions;
using Orbit.Domain.Access;

namespace Orbit.Api.Tenancy;

public sealed class CurrentPrincipal : ICurrentPrincipal
{
    private bool _initialized;

    public Guid MembershipId { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? SessionId { get; private set; }
    public PrincipalType PrincipalType { get; private set; }
    public TenantRole TenantRole { get; private set; }
    public MembershipTier MembershipTier { get; private set; }
    public bool IsDevelopmentBypass { get; private set; }

    public void SetMembership(TenantMembership membership, Guid? sessionId = null)
    {
        Set(
            membership.Id,
            membership.UserId,
            membership.PrincipalType,
            membership.Role,
            membership.Tier,
            isDevelopmentBypass: false);
        SessionId = sessionId;
    }

    /// <summary>
    /// Primitive-field overload for a cache-hit path (see <c>AuthorizationContextCache</c>) where
    /// no <see cref="TenantMembership"/> was loaded from the database this request.
    /// </summary>
    public void SetMembership(
        Guid membershipId,
        Guid? userId,
        PrincipalType principalType,
        TenantRole tenantRole,
        MembershipTier membershipTier,
        Guid? sessionId = null)
    {
        Set(membershipId, userId, principalType, tenantRole, membershipTier, isDevelopmentBypass: false);
        SessionId = sessionId;
    }

    public void SetDevelopmentPrincipal(Guid tenantId)
    {
        Set(tenantId, null, PrincipalType.User, TenantRole.Owner, MembershipTier.Standard, isDevelopmentBypass: true);
    }

    public void SetUser(Guid userId, PrincipalType principalType = PrincipalType.User, Guid? sessionId = null)
    {
        Set(Guid.Empty, userId, principalType, TenantRole.Member, MembershipTier.Standard, isDevelopmentBypass: false);
        SessionId = sessionId;
    }

    private void Set(
        Guid membershipId,
        Guid? userId,
        PrincipalType principalType,
        TenantRole tenantRole,
        MembershipTier membershipTier,
        bool isDevelopmentBypass)
    {
        if (_initialized)
        {
            throw new InvalidOperationException("Principal context is immutable for a request.");
        }

        MembershipId = membershipId;
        UserId = userId;
        PrincipalType = principalType;
        TenantRole = tenantRole;
        MembershipTier = membershipTier;
        IsDevelopmentBypass = isDevelopmentBypass;
        _initialized = true;
    }
}
