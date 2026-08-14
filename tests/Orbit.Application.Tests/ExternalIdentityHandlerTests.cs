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
        public bool IsDevelopmentBypass => false;
    }

    private sealed class TokenValidatorStub : IExternalIdentityTokenValidator
    {
        public Task<VerifiedExternalIdentity> ValidateAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult(new VerifiedExternalIdentity("https://idp.example.test", "subject-1"));
    }

    private sealed class RejectingTokenValidatorStub : IExternalIdentityTokenValidator
    {
        public Task<VerifiedExternalIdentity> ValidateAsync(string token, CancellationToken cancellationToken) =>
            throw new AuthenticationException("Invalid proof.");
    }

    private sealed class AuthRepositoryStub : IAuthenticationRepository
    {
        public List<ExternalIdentity> ExternalIdentities { get; } = [];

        public Task<UserAccount?> GetUserAccountAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserAccount?>(null);

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
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
