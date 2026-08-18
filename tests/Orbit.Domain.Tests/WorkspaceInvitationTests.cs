using Orbit.Domain.Access;
using Orbit.Domain.Common;

namespace Orbit.Domain.Tests;

public sealed class WorkspaceInvitationTests
{
    [Fact]
    public void Accept_IsSingleUseAndRecordsAccount()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = Create(now);
        var userId = Guid.NewGuid();

        invitation.Accept(userId, now.AddMinutes(1));

        Assert.Equal(WorkspaceInvitationStatus.Accepted, invitation.Status);
        Assert.Equal(userId, invitation.AcceptedByUserId);
        Assert.False(invitation.IsUsable(now.AddMinutes(1)));
        Assert.Throws<DomainException>(() => invitation.Accept(userId, now.AddMinutes(2)));
    }

    [Fact]
    public void Create_RejectsOwnerRole()
    {
        var action = () => WorkspaceInvitation.Create(
            Guid.NewGuid(),
            "user@example.test",
            TenantRole.Owner,
            null,
            "token-hash",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            TimeSpan.FromDays(7));

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Create_RejectsGuestTierWithAdministratorRole()
    {
        var action = () => WorkspaceInvitation.Create(
            Guid.NewGuid(),
            "user@example.test",
            TenantRole.Administrator,
            null,
            "token-hash",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            TimeSpan.FromDays(7),
            MembershipTier.Guest);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Create_AllowsGuestTierWithMemberRole()
    {
        var invitation = WorkspaceInvitation.Create(
            Guid.NewGuid(),
            "user@example.test",
            TenantRole.Member,
            null,
            "token-hash",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            TimeSpan.FromDays(7),
            MembershipTier.Guest);

        Assert.Equal(MembershipTier.Guest, invitation.Tier);
    }

    [Fact]
    public void Renew_RotatesTokenAndExpiry()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = Create(now);

        invitation.Renew(
            TenantRole.Administrator,
            Guid.NewGuid(),
            "new-token-hash",
            Guid.NewGuid(),
            now.AddDays(1),
            TimeSpan.FromDays(7));

        Assert.Equal("new-token-hash", invitation.TokenHash);
        Assert.Equal(TenantRole.Administrator, invitation.Role);
        Assert.Equal(now.AddDays(8), invitation.ExpiresAt);
        Assert.Equal(2, invitation.Version);
    }

    private static WorkspaceInvitation Create(DateTimeOffset now) =>
        WorkspaceInvitation.Create(
            Guid.NewGuid(),
            "user@example.test",
            TenantRole.Member,
            null,
            "token-hash",
            Guid.NewGuid(),
            now,
            TimeSpan.FromDays(7));
}
