using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.Identity;
using Orbit.Domain.Access;
using Orbit.Domain.Identity;
using Orbit.Domain.Organizations;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Tests;

public sealed class GoogleOAuthHandlerTests
{
    [Fact]
    public async Task Callback_LoginMode_WithExistingLink_CreatesHandoffForItsWorkspace()
    {
        var now = DateTimeOffset.UtcNow;
        var account = UserAccount.Create("member@example.test", "Member", now);
        var workspace = Workspace.Create(Guid.CreateVersion7(), "Workspace", now);
        var identity = ExternalIdentity.Create(account.Id, "https://accounts.google.com", "google-sub-1", now);
        var repository = new AuthRepositoryStub();
        repository.Account = account;
        repository.ExternalIdentities.Add(identity);
        repository.Memberships.Add(TenantMembership.CreateForUser(workspace.Id, account.Id, TenantRole.Owner, now));

        var handler = new HandleGoogleCallbackHandler(
            new GoogleOAuthClientStub(),
            new GoogleIdTokenValidatorStub(new VerifiedGoogleIdentity("google-sub-1", "member@example.test", true, "Member")),
            new PassthroughStateCodec(),
            repository,
            new SignUpRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(new HandleGoogleCallbackCommand("code", "login"), CancellationToken.None);

        Assert.NotEmpty(result.HandoffCode);
        var handoff = Assert.Single(repository.SignInHandoffs);
        Assert.Equal(account.Id, handoff.UserId);
        Assert.Equal(workspace.Id, handoff.TenantId);
    }

    [Fact]
    public async Task Callback_LoginMode_AutoLinksByVerifiedEmail_WhenNoExistingIdentity()
    {
        var now = DateTimeOffset.UtcNow;
        var account = UserAccount.Create("member@example.test", "Member", now);
        var workspace = Workspace.Create(Guid.CreateVersion7(), "Workspace", now);
        var repository = new AuthRepositoryStub();
        repository.Account = account;
        repository.Memberships.Add(TenantMembership.CreateForUser(workspace.Id, account.Id, TenantRole.Owner, now));

        var handler = new HandleGoogleCallbackHandler(
            new GoogleOAuthClientStub(),
            new GoogleIdTokenValidatorStub(new VerifiedGoogleIdentity("google-sub-2", "member@example.test", true, "Member")),
            new PassthroughStateCodec(),
            repository,
            new SignUpRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(new HandleGoogleCallbackCommand("code", "login"), CancellationToken.None);

        var linked = Assert.Single(repository.ExternalIdentities);
        Assert.Equal(account.Id, linked.UserId);
        Assert.Equal("google-sub-2", linked.Subject);
    }

    [Fact]
    public async Task Callback_LoginMode_ThrowsWhenNoMatchingAccountExists()
    {
        var repository = new AuthRepositoryStub();
        var handler = new HandleGoogleCallbackHandler(
            new GoogleOAuthClientStub(),
            new GoogleIdTokenValidatorStub(new VerifiedGoogleIdentity("google-sub-3", "nobody@example.test", true, "Nobody")),
            new PassthroughStateCodec(),
            repository,
            new SignUpRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(new HandleGoogleCallbackCommand("code", "login"), CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(action);
    }

    [Fact]
    public async Task Callback_RegisterMode_ProvisionsNewOrganizationAndWorkspace()
    {
        var repository = new AuthRepositoryStub();
        var signUpRepository = new SignUpRepositoryStub();
        var handler = new HandleGoogleCallbackHandler(
            new GoogleOAuthClientStub(),
            new GoogleIdTokenValidatorStub(new VerifiedGoogleIdentity("google-sub-4", "new@example.test", true, "New Person")),
            new PassthroughStateCodec(),
            repository,
            signUpRepository,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(new HandleGoogleCallbackCommand("code", "register"), CancellationToken.None);

        Assert.NotEmpty(result.HandoffCode);
        Assert.NotNull(signUpRepository.Organization);
        Assert.NotNull(signUpRepository.Workspace);
        Assert.Equal(signUpRepository.Organization!.Id, signUpRepository.Workspace!.OrganizationId);
        Assert.Equal(OrganizationRole.Owner, signUpRepository.OrganizationMembership!.Role);
        Assert.Equal("new@example.test", signUpRepository.Account!.NormalizedEmail);
    }

    [Fact]
    public async Task Callback_ThrowsWhenStateIsInvalid()
    {
        var repository = new AuthRepositoryStub();
        var handler = new HandleGoogleCallbackHandler(
            new GoogleOAuthClientStub(),
            new GoogleIdTokenValidatorStub(new VerifiedGoogleIdentity("sub", "a@b.test", true, "A")),
            new RejectingStateCodec(),
            repository,
            new SignUpRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(new HandleGoogleCallbackCommand("code", "bad-state"), CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(action);
    }

    [Fact]
    public async Task Exchange_ConsumesHandoffOnce_AndFailsOnReuse()
    {
        var now = DateTimeOffset.UtcNow;
        var account = UserAccount.Create("member@example.test", "Member", now);
        var workspace = Workspace.Create(Guid.CreateVersion7(), "Workspace", now);
        var repository = new AuthRepositoryStub();
        repository.Account = account;
        repository.Workspaces[workspace.Id] = workspace;
        repository.Memberships.Add(TenantMembership.CreateForUser(workspace.Id, account.Id, TenantRole.Owner, now));
        var handoff = GoogleSignInHandoff.Create(HandoffHashForTest("plain-code"), account.Id, workspace.Id, now);
        repository.SignInHandoffs.Add(handoff);

        var handler = new ExchangeGoogleHandoffHandler(
            repository, new AccessTokenIssuerStub(), new UnitOfWorkStub(), TimeProvider.System);

        var session = await handler.Handle(new ExchangeGoogleHandoffCommand("plain-code"), CancellationToken.None);
        Assert.Equal(workspace.Id, session.WorkspaceId);
        Assert.NotEmpty(session.RefreshToken);

        var action = () => handler.Handle(new ExchangeGoogleHandoffCommand("plain-code"), CancellationToken.None);
        await Assert.ThrowsAsync<AuthenticationException>(action);
    }

    private static string HandoffHashForTest(string plainCode)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(plainCode)));
    }

    private sealed class GoogleOAuthClientStub : IGoogleOAuthClient
    {
        public string BuildAuthorizeUrl(string state) => $"https://accounts.google.com/o/oauth2/v2/auth?state={state}";

        public Task<string> ExchangeCodeForIdTokenAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult("fake-id-token");
    }

    private sealed class GoogleIdTokenValidatorStub(VerifiedGoogleIdentity identity) : IGoogleIdTokenValidator
    {
        public Task<VerifiedGoogleIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken) =>
            Task.FromResult(identity);
    }

    private sealed class PassthroughStateCodec : IOAuthStateCodec
    {
        public string Encode(string mode, DateTimeOffset now, TimeSpan lifetime) => mode;

        public bool TryDecode(string state, DateTimeOffset now, out string mode)
        {
            mode = state;
            return true;
        }
    }

    private sealed class RejectingStateCodec : IOAuthStateCodec
    {
        public string Encode(string mode, DateTimeOffset now, TimeSpan lifetime) => mode;

        public bool TryDecode(string state, DateTimeOffset now, out string mode)
        {
            mode = string.Empty;
            return false;
        }
    }

    private sealed class AccessTokenIssuerStub : IAccessTokenIssuer
    {
        public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(1);
        public TimeSpan PersistentRefreshTokenLifetime => TimeSpan.FromDays(30);
        public string LocalIssuer => "urn:orbit:local";

        public AccessToken IssueUserToken(Guid userId, Guid tenantId, Guid sessionId, DateTimeOffset now) =>
            new($"access-{sessionId}", now.AddMinutes(15));

        public AccessToken IssueServiceAccountToken(Guid tenantId, string clientId, DateTimeOffset now) =>
            new($"access-{clientId}", now.AddMinutes(15));
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private sealed class SignUpRepositoryStub : ISignUpRepository
    {
        public UserAccount? Account { get; private set; }
        public Organization? Organization { get; private set; }
        public Workspace? Workspace { get; private set; }
        public OrganizationMembership? OrganizationMembership { get; private set; }
        public TenantMembership? OwnerMembership { get; private set; }

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task AddAsync(
            UserAccount account,
            LocalCredential credential,
            Organization organization,
            Workspace workspace,
            OrganizationMembership organizationMembership,
            TenantMembership ownerMembership,
            RefreshSession refreshSession,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ProvisionExternalAccountAsync(
            UserAccount account,
            ExternalIdentity identity,
            Organization organization,
            Workspace workspace,
            OrganizationMembership organizationMembership,
            TenantMembership ownerMembership,
            GoogleSignInHandoff handoff,
            CancellationToken cancellationToken)
        {
            Account = account;
            Organization = organization;
            Workspace = workspace;
            OrganizationMembership = organizationMembership;
            OwnerMembership = ownerMembership;
            return Task.CompletedTask;
        }
    }

    private sealed class AuthRepositoryStub : IAuthenticationRepository
    {
        public UserAccount? Account { get; set; }
        public List<ExternalIdentity> ExternalIdentities { get; } = [];
        public List<TenantMembership> Memberships { get; } = [];
        public Dictionary<Guid, Workspace> Workspaces { get; } = [];
        public List<GoogleSignInHandoff> SignInHandoffs { get; } = [];

        public Task<UserAccount?> GetUserAccountAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Account?.Id == userId ? Account : null);

        public Task<UserAccount?> GetUserAccountByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(Account?.NormalizedEmail == normalizedEmail ? Account : null);

        public Task<LocalCredential?> GetLocalCredentialAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<LocalCredential?>(null);

        public Task<IReadOnlyList<TenantMembership>> ListActiveMembershipsByUserAsync(
            Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>(
                Memberships.Where(membership => membership.UserId == userId).ToArray());

        public Task<Workspace?> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(Workspaces.GetValueOrDefault(tenantId));

        public Task<IReadOnlyList<Workspace>> GetWorkspacesAsync(
            IReadOnlyCollection<Guid> tenantIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Workspace>>(
                Workspaces.Values.Where(workspace => tenantIds.Contains(workspace.Id)).ToArray());

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

        public Task<ExternalIdentity?> GetExternalIdentityAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(ExternalIdentities.SingleOrDefault(identity => identity.Id == id && identity.UserId == userId));

        public Task RemoveExternalIdentityAsync(ExternalIdentity identity, CancellationToken cancellationToken)
        {
            ExternalIdentities.Remove(identity);
            return Task.CompletedTask;
        }

        public Task AddPasswordResetTokenAsync(PasswordResetToken token, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult<PasswordResetToken?>(null);

        public Task RevokeActivePasswordResetTokensForUserAsync(
            Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task UpdateLocalCredentialAsync(LocalCredential credential, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddServiceAccountCredentialAsync(ServiceAccountCredential credential, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<ServiceAccountCredential?> GetActiveServiceAccountCredentialByClientIdAsync(
            Guid clientId, CancellationToken cancellationToken) =>
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
}
