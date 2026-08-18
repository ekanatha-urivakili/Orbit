using Orbit.Domain.Access;
using Orbit.Domain.Common;
using Orbit.Domain.Identity;
using Orbit.Domain.Workspaces;

namespace Orbit.Domain.Tests;

public sealed class IdentityModelTests
{
    [Fact]
    public void UserAccount_CreateNormalizesEmailWithoutProviderSpecificRewriting()
    {
        var account = UserAccount.Create(
            "  First.Last+orbit@MÜNICH.example  ",
            "Orbit Admin",
            DateTimeOffset.UtcNow);

        Assert.Equal("first.last+orbit@xn--mnich-kva.example", account.NormalizedEmail);
        Assert.NotNull(account.EmailVerifiedAt);
    }

    [Fact]
    public void Workspace_CreateBuildsStableAsciiSlug()
    {
        var workspace = Workspace.Create(Guid.CreateVersion7(), "Éka's Product Team", DateTimeOffset.UtcNow);

        Assert.Equal("eka-s-product-team", workspace.Slug);
        Assert.Equal(1, workspace.AuthorizationEpoch);
    }

    [Fact]
    public void TenantMembership_CreateForUserLinksGlobalAccount()
    {
        var userId = Guid.NewGuid();
        var membership = TenantMembership.CreateForUser(
            Guid.NewGuid(),
            userId,
            TenantRole.Owner,
            DateTimeOffset.UtcNow);

        Assert.Equal(userId, membership.UserId);
        Assert.Null(membership.Issuer);
        Assert.Null(membership.Subject);
        Assert.Equal(PrincipalType.User, membership.PrincipalType);
    }

    [Fact]
    public void RefreshSession_RotationPreservesFamilyAndRevokesPrevious()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var initial = RefreshSession.CreateInitial(
            userId,
            tenantId,
            "hash-1",
            "orbit-tests",
            "127.0.0.1",
            false,
            now,
            TimeSpan.FromDays(30));

        var rotated = initial.CreateRotated(tenantId, "hash-2", "orbit-tests", "127.0.0.1", now.AddMinutes(5), TimeSpan.FromDays(30));
        initial.MarkRotated(rotated.Id, now.AddMinutes(5));

        Assert.Equal(initial.FamilyId, rotated.FamilyId);
        Assert.Equal(RefreshSessionStatus.Rotated, initial.Status);
        Assert.Equal(rotated.Id, initial.ReplacedBySessionId);
        Assert.Equal(2, initial.Version);
        Assert.False(initial.IsUsable(now.AddMinutes(5)));
        Assert.True(rotated.IsUsable(now.AddMinutes(5)));
    }

    [Fact]
    public void RefreshSession_ExpiredSessionIsNotUsable()
    {
        var now = DateTimeOffset.UtcNow;
        var session = RefreshSession.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash-1",
            null,
            null,
            false,
            now,
            TimeSpan.FromMinutes(1));

        Assert.True(session.IsUsable(now));
        Assert.False(session.IsUsable(now.AddMinutes(2)));
    }

    [Fact]
    public void RefreshSession_RevokeIsIdempotent()
    {
        var now = DateTimeOffset.UtcNow;
        var session = RefreshSession.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash-1",
            null,
            null,
            false,
            now,
            TimeSpan.FromDays(30));

        session.Revoke(now.AddMinutes(1));
        session.Revoke(now.AddMinutes(2));

        Assert.Equal(RefreshSessionStatus.Revoked, session.Status);
        Assert.Equal(now.AddMinutes(1), session.RevokedAt);
        Assert.Equal(2, session.Version);
    }

    [Fact]
    public void PasswordResetToken_ConsumeMarksUsedAndBumpsVersion()
    {
        var now = DateTimeOffset.UtcNow;
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash-1", now, TimeSpan.FromHours(1));

        token.Consume(now.AddMinutes(5));

        Assert.Equal(PasswordResetTokenStatus.Used, token.Status);
        Assert.Equal(now.AddMinutes(5), token.ConsumedAt);
        Assert.Equal(2, token.Version);
        Assert.False(token.IsUsable(now.AddMinutes(5)));
    }

    [Fact]
    public void PasswordResetToken_ExpiredTokenIsNotUsable()
    {
        var now = DateTimeOffset.UtcNow;
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash-1", now, TimeSpan.FromMinutes(1));

        Assert.True(token.IsUsable(now));
        Assert.False(token.IsUsable(now.AddMinutes(2)));
    }

    [Fact]
    public void PasswordResetToken_ConsumeThrowsWhenNotActive()
    {
        var now = DateTimeOffset.UtcNow;
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash-1", now, TimeSpan.FromHours(1));
        token.Revoke(now);

        var action = () => token.Consume(now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void PasswordResetToken_RevokeIsIdempotent()
    {
        var now = DateTimeOffset.UtcNow;
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash-1", now, TimeSpan.FromHours(1));

        token.Revoke(now.AddMinutes(1));
        token.Revoke(now.AddMinutes(2));

        Assert.Equal(PasswordResetTokenStatus.Revoked, token.Status);
        Assert.Equal(now.AddMinutes(1), token.ConsumedAt);
        Assert.Equal(2, token.Version);
    }

    [Fact]
    public void LocalCredential_UpdatePasswordChangesHashAndTimestamp()
    {
        var now = DateTimeOffset.UtcNow;
        var credential = LocalCredential.Create(Guid.NewGuid(), "hash-1", "argon2id", 1, now);

        credential.UpdatePassword("hash-2", "argon2id", 2, now.AddMinutes(5));

        Assert.Equal("hash-2", credential.PasswordHash);
        Assert.Equal(2, credential.HashParametersVersion);
        Assert.Equal(now.AddMinutes(5), credential.ChangedAt);
    }

    [Fact]
    public void ExternalIdentity_Create_RejectsEmptyUserId()
    {
        var action = () => ExternalIdentity.Create(
            Guid.Empty, "https://idp.example.test", "subject-1", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void ExternalIdentity_Create_RejectsEmptyIssuerOrSubject()
    {
        var action = () => ExternalIdentity.Create(Guid.NewGuid(), "  ", "subject-1", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void ExternalIdentity_Create_TrimsIssuerAndSubject()
    {
        var identity = ExternalIdentity.Create(
            Guid.NewGuid(), "  https://idp.example.test  ", "  subject-1  ", DateTimeOffset.UtcNow);

        Assert.Equal("https://idp.example.test", identity.Issuer);
        Assert.Equal("subject-1", identity.Subject);
    }
}
