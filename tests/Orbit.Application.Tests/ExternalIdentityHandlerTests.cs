using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.Identity;
using Orbit.Domain.Access;
using Orbit.Domain.Identity;

namespace Orbit.Application.Tests;

public sealed class ExternalIdentityHandlerTests
{
    [Fact]
    public async Task Link_CreatesNewIdentity_ForCurrentUser()
    {
        var userId = Guid.NewGuid();
        var repository = new AuthRepositoryStub();
        var handler = new LinkExternalIdentityHandler(
            new CurrentPrincipalStub(userId), new TokenValidatorStub(), repository, new UnitOfWorkStub(), TimeProvider.System);

        var result = await handler.Handle(
            new LinkExternalIdentityCommand("valid-proof"), CancellationToken.None);

        Assert.Equal("subject-1", result.Subject);
        Assert.Single(repository.ExternalIdentities);
        Assert.Equal(userId, repository.ExternalIdentities[0].UserId);
    }

    [Fact]
    public async Task Link_IsIdempotent_WhenAlreadyLinkedToSameUser()
    {
        var userId = Guid.NewGuid();
        var repository = new AuthRepositoryStub();
        repository.ExternalIdentities.Add(
            ExternalIdentity.Create(userId, "https://idp.example.test", "subject-1", DateTimeOffset.UtcNow));
        var handler = new LinkExternalIdentityHandler(
            new CurrentPrincipalStub(userId), new TokenValidatorStub(), repository, new UnitOfWorkStub(), TimeProvider.System);

        var result = await handler.Handle(
            new LinkExternalIdentityCommand("valid-proof"), CancellationToken.None);

        Assert.Equal("subject-1", result.Subject);
        Assert.Single(repository.ExternalIdentities);
    }

    [Fact]
    public async Task Link_RejectsIdentityAlreadyLinkedToAnotherUser()
    {
        var repository = new AuthRepositoryStub();
        repository.ExternalIdentities.Add(
            ExternalIdentity.Create(Guid.NewGuid(), "https://idp.example.test", "subject-1", DateTimeOffset.UtcNow));
        var handler = new LinkExternalIdentityHandler(
            new CurrentPrincipalStub(Guid.NewGuid()), new TokenValidatorStub(), repository, new UnitOfWorkStub(), TimeProvider.System);

        var action = () => handler.Handle(
            new LinkExternalIdentityCommand("valid-proof"), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(action);
    }

    [Fact]
    public async Task Link_DoesNotPersistAnUnverifiedIdentity()
    {
        var repository = new AuthRepositoryStub();
        var handler = new LinkExternalIdentityHandler(
            new CurrentPrincipalStub(Guid.NewGuid()),
            new RejectingTokenValidatorStub(),
            repository,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(new LinkExternalIdentityCommand("invalid-proof"), CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(action);
        Assert.Empty(repository.ExternalIdentities);
    }

    [Fact]
    public async Task Link_AllowsMatchingVerifiedEmail()
    {
        var userId = Guid.NewGuid();
        var account = UserAccount.Create("person@example.test", "Person", DateTimeOffset.UtcNow);
        var repository = new AuthRepositoryStub { Account = account };
        var handler = new LinkExternalIdentityHandler(
            new CurrentPrincipalStub(userId),
            new TokenValidatorStub("Person@Example.Test", emailVerified: true),
            repository,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(new LinkExternalIdentityCommand("valid-proof"), CancellationToken.None);

        Assert.Equal("subject-1", result.Subject);
        Assert.Single(repository.ExternalIdentities);
    }

    [Fact]
    public async Task Link_RejectsMismatchedVerifiedEmail()
    {
        var userId = Guid.NewGuid();
        var account = UserAccount.Create("person@example.test", "Person", DateTimeOffset.UtcNow);
        var repository = new AuthRepositoryStub { Account = account };
        var handler = new LinkExternalIdentityHandler(
            new CurrentPrincipalStub(userId),
            new TokenValidatorStub("someone-else@example.test", emailVerified: true),
            repository,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(new LinkExternalIdentityCommand("valid-proof"), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(action);
        Assert.Empty(repository.ExternalIdentities);
    }

    [Fact]
    public async Task List_ReturnsOnlyTheCurrentUsersIdentities()
    {
        var userId = Guid.NewGuid();
        var repository = new AuthRepositoryStub();
        repository.ExternalIdentities.Add(
            ExternalIdentity.Create(userId, "https://idp.example.test", "subject-1", DateTimeOffset.UtcNow));
        repository.ExternalIdentities.Add(
            ExternalIdentity.Create(Guid.NewGuid(), "https://idp.example.test", "subject-2", DateTimeOffset.UtcNow));
        var handler = new ListLinkedIdentitiesHandler(new CurrentPrincipalStub(userId), repository);

        var result = await handler.Handle(new ListLinkedIdentitiesQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("subject-1", result[0].Subject);
    }

    [Fact]
    public async Task Unlink_RemovesTheIdentity()
    {
        var userId = Guid.NewGuid();
        var identity = ExternalIdentity.Create(userId, "https://idp.example.test", "subject-1", DateTimeOffset.UtcNow);
        var repository = new AuthRepositoryStub();
        repository.ExternalIdentities.Add(identity);
        var handler = new UnlinkExternalIdentityHandler(
            new CurrentPrincipalStub(userId), repository, new UnitOfWorkStub());

        await handler.Handle(new UnlinkExternalIdentityCommand(identity.Id), CancellationToken.None);

        Assert.Empty(repository.ExternalIdentities);
    }

    [Fact]
    public async Task Unlink_ThrowsNotFound_ForAnotherUsersIdentity()
    {
        var identity = ExternalIdentity.Create(
            Guid.NewGuid(), "https://idp.example.test", "subject-1", DateTimeOffset.UtcNow);
        var repository = new AuthRepositoryStub();
        repository.ExternalIdentities.Add(identity);
        var handler = new UnlinkExternalIdentityHandler(
            new CurrentPrincipalStub(Guid.NewGuid()), repository, new UnitOfWorkStub());

        var action = () => handler.Handle(new UnlinkExternalIdentityCommand(identity.Id), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    private sealed class CurrentPrincipalStub(Guid userId) : ICurrentPrincipal
    {
        public Guid? UserId => userId;
        public Guid? SessionId => null;
        public Guid MembershipId => Guid.NewGuid();
        public PrincipalType PrincipalType => PrincipalType.User;
        public TenantRole TenantRole => TenantRole.Member;
        public MembershipTier MembershipTier => MembershipTier.Standard;
        public bool IsDevelopmentBypass => false;
    }

    private sealed class TokenValidatorStub(string? email = null, bool emailVerified = false)
        : IExternalIdentityTokenValidator
    {
        public Task<VerifiedExternalIdentity> ValidateAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult(new VerifiedExternalIdentity("https://idp.example.test", "subject-1", email, emailVerified));
    }

    private sealed class RejectingTokenValidatorStub : IExternalIdentityTokenValidator
    {
        public Task<VerifiedExternalIdentity> ValidateAsync(string token, CancellationToken cancellationToken) =>
            throw new AuthenticationException("Invalid proof.");
    }

    private sealed class AuthRepositoryStub : IAuthenticationRepository
    {
        public List<GoogleSignInHandoff> SignInHandoffs { get; } = [];

        public List<ExternalIdentity> ExternalIdentities { get; } = [];
        public UserAccount? Account { get; set; }

        public Task<UserAccount?> GetUserAccountAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Account);

        public Task<UserAccount?> GetUserAccountByEmailAsync(
            string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult<UserAccount?>(null);

        public Task<LocalCredential?> GetLocalCredentialAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<LocalCredential?>(null);

        public Task<IReadOnlyList<TenantMembership>> ListActiveMembershipsByUserAsync(
            Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<Domain.Workspaces.Workspace?> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<Domain.Workspaces.Workspace?>(null);

        public Task<IReadOnlyList<Domain.Workspaces.Workspace>> GetWorkspacesAsync(
            IReadOnlyCollection<Guid> tenantIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Domain.Workspaces.Workspace>>([]);

        public Task AddRefreshSessionAsync(RefreshSession session, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<RefreshSession?> GetRefreshSessionByTokenHashAsync(
            string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult<RefreshSession?>(null);

        public Task<RefreshSession?> GetActiveSessionAsync(
            Guid sessionId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<RefreshSession?>(null);

        public Task<IReadOnlyList<RefreshSession>> ListActiveSessionsByUserAsync(
            Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RefreshSession>>([]);

        public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddExternalIdentityAsync(ExternalIdentity identity, CancellationToken cancellationToken)
        {
            ExternalIdentities.Add(identity);
            return Task.CompletedTask;
        }

        public Task<ExternalIdentity?> GetExternalIdentityAsync(
            string issuer, string subject, CancellationToken cancellationToken) =>
            Task.FromResult(ExternalIdentities.SingleOrDefault(
                identity => identity.Issuer == issuer && identity.Subject == subject));

        public Task<IReadOnlyList<ExternalIdentity>> ListExternalIdentitiesByUserAsync(
            Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExternalIdentity>>(
                ExternalIdentities.Where(identity => identity.UserId == userId).ToArray());

        public Task<ExternalIdentity?> GetExternalIdentityAsync(
            Guid id, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(ExternalIdentities.SingleOrDefault(
                identity => identity.Id == id && identity.UserId == userId));

        public Task RemoveExternalIdentityAsync(ExternalIdentity identity, CancellationToken cancellationToken)
        {
            ExternalIdentities.Remove(identity);
            return Task.CompletedTask;
        }

        public Task AddPasswordResetTokenAsync(PasswordResetToken token, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(
            string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult<PasswordResetToken?>(null);

        public Task RevokeActivePasswordResetTokensForUserAsync(
            Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task UpdateLocalCredentialAsync(LocalCredential credential, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddServiceAccountCredentialAsync(ServiceAccountCredential credential, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<ServiceAccountCredential?> GetActiveServiceAccountCredentialByClientIdAsync(Guid clientId, CancellationToken cancellationToken) =>
            Task.FromResult<ServiceAccountCredential?>(null);

        public Task<IReadOnlyList<ServiceAccountCredential>> ListActiveServiceAccountCredentialsByMembershipAsync(
            Guid membershipId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ServiceAccountCredential>>([]);

        public Task<TenantMembership?> GetActiveServiceAccountMembershipAsync(
            Guid tenantId, Guid membershipId, CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task AddSignInHandoffAsync(GoogleSignInHandoff handoff, CancellationToken cancellationToken)
        {
            SignInHandoffs.Add(handoff);
            return Task.CompletedTask;
        }

        public Task<GoogleSignInHandoff?> ConsumeSignInHandoffAsync(
            string codeHash, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var handoff = SignInHandoffs.SingleOrDefault(candidate => candidate.CodeHash == codeHash);
            if (handoff is null) return Task.FromResult<GoogleSignInHandoff?>(null);
            SignInHandoffs.Remove(handoff);
            return Task.FromResult(handoff.IsUsable(now) ? handoff : null);
        }

    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
