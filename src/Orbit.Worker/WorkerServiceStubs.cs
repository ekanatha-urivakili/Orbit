using Orbit.Application.Abstractions;
using Orbit.Domain.Access;

namespace Orbit.Worker;

/// <summary>
/// The worker only ever touches global tables (outbox, password reset), which carry no tenant
/// query filter, so this value is never actually read - it exists because
/// <c>OrbitDbContext</c>'s constructor requires an <see cref="ITenantContext"/>, same as the
/// design-time migration factory's stand-in.
/// </summary>
internal sealed class WorkerTenantContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;
}

/// <summary>
/// <c>AddInfrastructure</c> also registers several request-scoped repositories/authorization
/// services that depend on <see cref="ICurrentPrincipal"/>, even though the worker's own
/// <see cref="Orbit.Infrastructure.Messaging.OutboxEmailProcessor"/> never resolves them - this
/// stub only exists to satisfy DI's build-time validation, never to be read.
/// </summary>
internal sealed class WorkerCurrentPrincipal : ICurrentPrincipal
{
    public Guid? UserId => null;
    public Guid? SessionId => null;
    public Guid MembershipId => Guid.Empty;
    public PrincipalType PrincipalType => PrincipalType.ServiceAccount;
    public TenantRole TenantRole => TenantRole.Member;
    public MembershipTier MembershipTier => MembershipTier.Standard;
    public bool IsDevelopmentBypass => false;
}
