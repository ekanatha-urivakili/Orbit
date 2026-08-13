using Orbit.Domain.Access;
using Orbit.Domain.Common;

namespace Orbit.Domain.Tests;

public sealed class AccessModelTests
{
    [Fact]
    public void TenantMembership_CreatePreservesStableExternalIdentity()
    {
        var membership = TenantMembership.Create(
            Guid.NewGuid(),
            "https://identity.example.test",
            "oidc-subject",
            PrincipalType.ServiceAccount,
            TenantRole.Member,
            DateTimeOffset.UtcNow);

        Assert.Equal("https://identity.example.test", membership.Issuer);
        Assert.Equal("oidc-subject", membership.Subject);
        Assert.Equal(PrincipalType.ServiceAccount, membership.PrincipalType);
        Assert.True(membership.IsActive);
    }

    [Fact]
    public void ProjectRoleAssignment_RejectsCrossBoundaryEmptyIdentifiers()
    {
        var action = () => ProjectRoleAssignment.Create(
            Guid.NewGuid(),
            Guid.Empty,
            Guid.NewGuid(),
            ProjectRole.Member,
            DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void TenantMembership_ChangeRole_UpdatesActiveMembership()
    {
        var membership = TenantMembership.CreateForUser(
            Guid.NewGuid(), Guid.NewGuid(), TenantRole.Member, DateTimeOffset.UtcNow);

        membership.ChangeRole(TenantRole.Administrator);

        Assert.Equal(TenantRole.Administrator, membership.Role);
    }

    [Fact]
    public void TenantMembership_ChangeRole_RejectsInactiveMembership()
    {
        var membership = TenantMembership.CreateForUser(
            Guid.NewGuid(), Guid.NewGuid(), TenantRole.Member, DateTimeOffset.UtcNow);
        membership.Deactivate();

        var action = () => membership.ChangeRole(TenantRole.Administrator);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void TenantMembership_Deactivate_ClearsActiveFlag()
    {
        var membership = TenantMembership.CreateForUser(
            Guid.NewGuid(), Guid.NewGuid(), TenantRole.Member, DateTimeOffset.UtcNow);

        membership.Deactivate();

        Assert.False(membership.IsActive);
    }
}
