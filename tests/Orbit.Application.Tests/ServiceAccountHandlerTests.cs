using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.Identity;
using Orbit.Domain.Access;
using Orbit.Domain.Identity;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Tests;

public sealed class ServiceAccountHandlerTests
{
    [Fact]
    public async Task Create_PersistsMembershipAndCredentialAndReturnsSecretOnce()
    {
        var tenantId = Guid.NewGuid();
        var memberships = new MembershipRepositoryStub();
        var authentication = new AuthenticationRepositoryStub(memberships.Memberships);
        var handler = new CreateServiceAccountHandler(
            new TenantContextStub(tenantId),
            new AuthorizationStub(true),
            memberships,
            authentication,
            new AccessTokenIssuerStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(new CreateServiceAccountCommand(TenantRole.Member), CancellationToken.None);

        Assert.NotEmpty(result.ClientSecret);
        Assert.Single(memberships.Memberships);
        Assert.Single(authentication.Credentials);
        Assert.Equal(memberships.Memberships[0].Id, result.MembershipId);
        Assert.Equal(memberships.Memberships[0].Subject, result.ClientId);
    }

    [Fact]
    public async Task Create_RejectsInsufficientPermission()
    {
        var handler = new CreateServiceAccountHandler(
            new TenantContextStub(Guid.NewGuid()),
            new AuthorizationStub(false),
            new MembershipRepositoryStub(),
            new AuthenticationRepositoryStub([]),
            new AccessTokenIssuerStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(new CreateServiceAccountCommand(TenantRole.Member), CancellationToken.None);

        await Assert.ThrowsAsync<AccessDeniedException>(action);
    }

    [Fact]
    public async Task Rotate_RevokesOldCredentialAndIssuesNewSecret()
    {
        var tenantId = Guid.NewGuid();
        var memberships = new MembershipRepositoryStub();
        var authentication = new AuthenticationRepositoryStub(memberships.Memberships);
        var createHandler = new CreateServiceAccountHandler(
            new TenantContextStub(tenantId),
            new AuthorizationStub(true),
            memberships,
            authentication,
            new AccessTokenIssuerStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);
        var created = await createHandler.Handle(new CreateServiceAccountCommand(TenantRole.Member), CancellationToken.None);
        var originalCredential = authentication.Credentials.Single();

        var rotateHandler = new RotateServiceAccountCredentialHandler(
            new TenantContextStub(tenantId),
            new AuthorizationStub(true),
            memberships,
            authentication,
            new UnitOfWorkStub(),
            TimeProvider.System);
        var rotated = await rotateHandler.Handle(
            new RotateServiceAccountCredentialCommand(created.MembershipId), CancellationToken.None);

        Assert.False(originalCredential.IsActive);
        Assert.NotEqual(created.ClientSecret, rotated.ClientSecret);
        Assert.Equal(2, authentication.Credentials.Count);
        Assert.Single(authentication.Credentials, credential => credential.IsActive);
    }

    [Fact]
    public async Task Rotate_RejectsUnknownMembership()
    {
        var tenantId = Guid.NewGuid();
        var handler = new RotateServiceAccountCredentialHandler(
            new TenantContextStub(tenantId),
            new AuthorizationStub(true),
            new MembershipRepositoryStub(),
            new AuthenticationRepositoryStub([]),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(new RotateServiceAccountCredentialCommand(Guid.NewGuid()), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    [Fact]
    public async Task IssueToken_ReturnsAccessTokenForValidCredential()
    {
        var tenantId = Guid.NewGuid();
        var memberships = new MembershipRepositoryStub();
        var authentication = new AuthenticationRepositoryStub(memberships.Memberships);
        var createHandler = new CreateServiceAccountHandler(
            new TenantContextStub(tenantId),
            new AuthorizationStub(true),
            memberships,
            authentication,
            new AccessTokenIssuerStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);
        var created = await createHandler.Handle(new CreateServiceAccountCommand(TenantRole.Member), CancellationToken.None);

        var tokenHandler = new IssueServiceAccountTokenHandler(authentication, new AccessTokenIssuerStub(), TimeProvider.System);
        var token = await tokenHandler.Handle(
            new IssueServiceAccountTokenCommand(created.ClientId, created.ClientSecret), CancellationToken.None);

        Assert.NotEmpty(token.AccessToken);
        Assert.Equal("Bearer", token.TokenType);
    }

    [Fact]
    public async Task IssueToken_RejectsWrongSecret()
    {
        var tenantId = Guid.NewGuid();
        var memberships = new MembershipRepositoryStub();
        var authentication = new AuthenticationRepositoryStub(memberships.Memberships);
        var createHandler = new CreateServiceAccountHandler(
            new TenantContextStub(tenantId),
            new AuthorizationStub(true),
            memberships,
            authentication,
            new AccessTokenIssuerStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);
        var created = await createHandler.Handle(new CreateServiceAccountCommand(TenantRole.Member), CancellationToken.None);

        var tokenHandler = new IssueServiceAccountTokenHandler(authentication, new AccessTokenIssuerStub(), TimeProvider.System);
        var action = () => tokenHandler.Handle(
            new IssueServiceAccountTokenCommand(created.ClientId, "wrong-secret"), CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(action);
    }

    [Fact]
    public async Task IssueToken_RejectsRevokedCredential()
    {
        var tenantId = Guid.NewGuid();
        var memberships = new MembershipRepositoryStub();
        var authentication = new AuthenticationRepositoryStub(memberships.Memberships);
        var createHandler = new CreateServiceAccountHandler(
            new TenantContextStub(tenantId),
            new AuthorizationStub(true),
            memberships,
            authentication,
            new AccessTokenIssuerStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);
        var created = await createHandler.Handle(new CreateServiceAccountCommand(TenantRole.Member), CancellationToken.None);
        authentication.Credentials.Single().Revoke(DateTimeOffset.UtcNow);

        var tokenHandler = new IssueServiceAccountTokenHandler(authentication, new AccessTokenIssuerStub(), TimeProvider.System);
        var action = () => tokenHandler.Handle(
            new IssueServiceAccountTokenCommand(created.ClientId, created.ClientSecret), CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(action);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class AuthorizationStub(bool allowed) : ITenantAuthorization
    {
        public bool CanCreateProject() => allowed;
        public bool CanCreateMembership(TenantRole role) => allowed;
        public bool CanManageTeams() => allowed;
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private sealed class AccessTokenIssuerStub : IAccessTokenIssuer
    {
        public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(30);

        public TimeSpan PersistentRefreshTokenLifetime => TimeSpan.FromDays(30);
        public string LocalIssuer => "urn:orbit:local";

        public AccessToken IssueUserToken(Guid userId, Guid tenantId, Guid sessionId, DateTimeOffset now) =>
            new($"user-{sessionId}", now.AddMinutes(15));

        public AccessToken IssueServiceAccountToken(Guid tenantId, string clientId, DateTimeOffset now) =>
            new($"service-{clientId}", now.AddMinutes(15));
    }

    private sealed class MembershipRepositoryStub : ITenantMembershipRepository
    {
        public List<TenantMembership> Memberships { get; } = [];

        public Task AddAsync(TenantMembership membership, CancellationToken cancellationToken)
        {
            Memberships.Add(membership);
            return Task.CompletedTask;
        }

        public Task<TenantMembership?> GetActiveAsync(
            Guid tenantId, string issuer, string subject, CancellationToken cancellationToken) =>
            Task.FromResult(Memberships.SingleOrDefault(
                membership => membership.TenantId == tenantId
                    && membership.Issuer == issuer
                    && membership.Subject == subject
                    && membership.IsActive));

        public Task<TenantMembership?> GetActiveByUserAsync(
            Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<TenantMembership?> GetActiveAsync(
            Guid tenantId, Guid membershipId, CancellationToken cancellationToken) =>
            Task.FromResult(Memberships.SingleOrDefault(
                membership => membership.TenantId == tenantId && membership.Id == membershipId && membership.IsActive));

        public Task<TenantMembership?> GetOwnerAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<IReadOnlyList<TenantMembership>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>(
                Memberships.Where(membership => membership.TenantId == tenantId).ToArray());

        public Task<IReadOnlyList<TenantMembership>> ListByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> membershipIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>(
                Memberships.Where(m => m.TenantId == tenantId && membershipIds.Contains(m.Id)).ToArray());
    }

    private sealed class AuthenticationRepositoryStub(List<TenantMembership> memberships) : IAuthenticationRepository
    {
        public List<GoogleSignInHandoff> SignInHandoffs { get; } = [];

        public List<ServiceAccountCredential> Credentials { get; } = [];

        public Task<UserAccount?> GetUserAccountAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserAccount?>(null);

        public Task<UserAccount?> GetUserAccountByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult<UserAccount?>(null);

        public Task<LocalCredential?> GetLocalCredentialAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<LocalCredential?>(null);

        public Task<IReadOnlyList<TenantMembership>> ListActiveMembershipsByUserAsync(
            Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<Workspace?> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<Workspace?>(null);

        public Task<IReadOnlyList<Workspace>> GetWorkspacesAsync(
            IReadOnlyCollection<Guid> tenantIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Workspace>>([]);

        public Task AddRefreshSessionAsync(RefreshSession session, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<RefreshSession?> GetRefreshSessionByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult<RefreshSession?>(null);

        public Task<RefreshSession?> GetActiveSessionAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<RefreshSession?>(null);

        public Task<IReadOnlyList<RefreshSession>> ListActiveSessionsByUserAsync(
            Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RefreshSession>>([]);

        public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddExternalIdentityAsync(ExternalIdentity identity, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<ExternalIdentity?> GetExternalIdentityAsync(
            string issuer, string subject, CancellationToken cancellationToken) =>
            Task.FromResult<ExternalIdentity?>(null);

        public Task<IReadOnlyList<ExternalIdentity>> ListExternalIdentitiesByUserAsync(
            Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExternalIdentity>>([]);

        public Task<ExternalIdentity?> GetExternalIdentityAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<ExternalIdentity?>(null);

        public Task RemoveExternalIdentityAsync(ExternalIdentity identity, CancellationToken cancellationToken) =>
            Task.CompletedTask;

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

        public Task AddServiceAccountCredentialAsync(ServiceAccountCredential credential, CancellationToken cancellationToken)
        {
            Credentials.Add(credential);
            return Task.CompletedTask;
        }

        public Task<ServiceAccountCredential?> GetActiveServiceAccountCredentialByClientIdAsync(
            Guid clientId, CancellationToken cancellationToken) =>
            Task.FromResult(Credentials.SingleOrDefault(
                credential => credential.ClientId == clientId && credential.IsActive));

        public Task<IReadOnlyList<ServiceAccountCredential>> ListActiveServiceAccountCredentialsByMembershipAsync(
            Guid membershipId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ServiceAccountCredential>>(
                Credentials.Where(credential => credential.MembershipId == membershipId && credential.IsActive).ToArray());

        public Task<TenantMembership?> GetActiveServiceAccountMembershipAsync(
            Guid tenantId, Guid membershipId, CancellationToken cancellationToken) =>
            Task.FromResult(memberships.SingleOrDefault(
                membership => membership.TenantId == tenantId
                    && membership.Id == membershipId
                    && membership.IsActive
                    && membership.PrincipalType == PrincipalType.ServiceAccount));

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
}
