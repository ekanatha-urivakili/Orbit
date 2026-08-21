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
            Guid.NewGuid(),
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

    [Fact]
    public void TenantMembership_CreateForUser_DefaultsToStandardTier()
    {
        var membership = TenantMembership.CreateForUser(
            Guid.NewGuid(), Guid.NewGuid(), TenantRole.Member, DateTimeOffset.UtcNow);

        Assert.Equal(MembershipTier.Standard, membership.Tier);
    }

    [Theory]
    [InlineData(TenantRole.Owner)]
    [InlineData(TenantRole.Administrator)]
    public void TenantMembership_CreateForUser_RejectsGuestWithElevatedRole(TenantRole role)
    {
        var action = () => TenantMembership.CreateForUser(
            Guid.NewGuid(), Guid.NewGuid(), role, DateTimeOffset.UtcNow, MembershipTier.Guest);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void TenantMembership_CreateForUser_AllowsGuestWithMemberRole()
    {
        var membership = TenantMembership.CreateForUser(
            Guid.NewGuid(), Guid.NewGuid(), TenantRole.Member, DateTimeOffset.UtcNow, MembershipTier.Guest);

        Assert.Equal(MembershipTier.Guest, membership.Tier);
    }

    [Fact]
    public void TenantMembership_ChangeRole_RejectsPromotingAGuestToAdministrator()
    {
        var membership = TenantMembership.CreateForUser(
            Guid.NewGuid(), Guid.NewGuid(), TenantRole.Member, DateTimeOffset.UtcNow, MembershipTier.Guest);

        var action = () => membership.ChangeRole(TenantRole.Administrator);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void TenantMembership_ChangeTier_RejectsPromotingAnAdministratorToGuest()
    {
        var membership = TenantMembership.CreateForUser(
            Guid.NewGuid(), Guid.NewGuid(), TenantRole.Administrator, DateTimeOffset.UtcNow);

        var action = () => membership.ChangeTier(MembershipTier.Guest);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void TenantMembership_ChangeTier_AllowsGuestForAMember()
    {
        var membership = TenantMembership.CreateForUser(
            Guid.NewGuid(), Guid.NewGuid(), TenantRole.Member, DateTimeOffset.UtcNow);

        membership.ChangeTier(MembershipTier.Guest);

        Assert.Equal(MembershipTier.Guest, membership.Tier);
    }
}
