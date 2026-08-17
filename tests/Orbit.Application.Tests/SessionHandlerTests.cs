using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.Identity;
using Orbit.Domain.Access;
using Orbit.Domain.Identity;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Tests;

public sealed class SessionHandlerTests
{
    private const string StoredHash = "argon2-encoded-hash";
    private const string CorrectPassword = "CorrectHorseBattery9";

    [Fact]
    public async Task Login_Succeeds_AndSelectsEarliestActiveMembershipByDefault()
    {
        var now = DateTimeOffset.UtcNow;
        var account = UserAccount.Create("member@example.test", "Member One", now);
        var credential = LocalCredential.Create(account.Id, StoredHash, "Argon2id", 1, now);
        var older = Workspace.Create(Guid.CreateVersion7(), "Older Workspace", now.AddDays(-2));
        var newer = Workspace.Create(Guid.CreateVersion7(), "Newer Workspace", now.AddDays(-1));
        var repository = new AuthRepositoryStub();
        repository.Account = account;
        repository.Credential = credential;
        repository.Workspaces[older.Id] = older;
        repository.Workspaces[newer.Id] = newer;
        repository.Memberships.Add(
            TenantMembership.CreateForUser(newer.Id, account.Id, TenantRole.Member, now.AddDays(-1)));
        repository.Memberships.Add(
            TenantMembership.CreateForUser(older.Id, account.Id, TenantRole.Owner, now.AddDays(-2)));
        var unitOfWork = new UnitOfWorkStub();
        var handler = new LoginHandler(
            repository,
            new PasswordHasherStub(StoredHash, CorrectPassword),
            new AccessTokenIssuerStub(),
            unitOfWork,
            TimeProvider.System);

        var result = await handler.Handle(
            new LoginCommand("Member@Example.test", CorrectPassword, null, "orbit-tests", "127.0.0.1"),
            CancellationToken.None);

        Assert.Equal(older.Id, result.WorkspaceId);
        Assert.Equal(TenantRole.Owner, result.Role);
        Assert.Single(repository.Sessions);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.NotEmpty(result.RefreshToken);
        Assert.NotEmpty(result.AccessToken);
    }

    [Fact]
    public async Task Login_SelectsRequestedWorkspace_WhenProvided()
    {
        var now = DateTimeOffset.UtcNow;
        var account = UserAccount.Create("member@example.test", "Member One", now);
        var credential = LocalCredential.Create(account.Id, StoredHash, "Argon2id", 1, now);
        var older = Workspace.Create(Guid.CreateVersion7(), "Older Workspace", now.AddDays(-2));
        var newer = Workspace.Create(Guid.CreateVersion7(), "Newer Workspace", now.AddDays(-1));
        var repository = new AuthRepositoryStub();
        repository.Account = account;
        repository.Credential = credential;
        repository.Workspaces[older.Id] = older;
        repository.Workspaces[newer.Id] = newer;
        repository.Memberships.Add(TenantMembership.CreateForUser(newer.Id, account.Id, TenantRole.Member, now.AddDays(-1)));
        repository.Memberships.Add(TenantMembership.CreateForUser(older.Id, account.Id, TenantRole.Owner, now.AddDays(-2)));
        var handler = new LoginHandler(
            repository,
            new PasswordHasherStub(StoredHash, CorrectPassword),
            new AccessTokenIssuerStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new LoginCommand("member@example.test", CorrectPassword, newer.Id, null, null),
            CancellationToken.None);

        Assert.Equal(newer.Id, result.WorkspaceId);
    }

    [Fact]
    public async Task Login_UnknownEmail_StillVerifiesAndThrowsGenericMessage()
    {
        var repository = new AuthRepositoryStub();
        var passwordHasher = new PasswordHasherStub(StoredHash, CorrectPassword);
        var handler = new LoginHandler(
            repository,
            passwordHasher,
            new AccessTokenIssuerStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new LoginCommand("nobody@example.test", CorrectPassword, null, null, null),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<AuthenticationException>(action);
        Assert.Equal("Invalid email or password.", exception.Message);
        Assert.Equal(1, passwordHasher.VerifyCount);
    }

    [Fact]
    public async Task Login_WrongPassword_Throws()
    {
        var now = DateTimeOffset.UtcNow;
        var account = UserAccount.Create("member@example.test", "Member One", now);
        var credential = LocalCredential.Create(account.Id, StoredHash, "Argon2id", 1, now);
        var repository = new AuthRepositoryStub { Account = account, Credential = credential };
        var handler = new LoginHandler(
            repository,
            new PasswordHasherStub(StoredHash, CorrectPassword),
            new AccessTokenIssuerStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new LoginCommand("member@example.test", "wrong-password", null, null, null),
            CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(action);
    }

    [Fact]
    public async Task Login_NoActiveMembership_ThrowsAccessDenied()
    {
        var now = DateTimeOffset.UtcNow;
        var account = UserAccount.Create("member@example.test", "Member One", now);
        var credential = LocalCredential.Create(account.Id, StoredHash, "Argon2id", 1, now);
        var repository = new AuthRepositoryStub { Account = account, Credential = credential };
        var handler = new LoginHandler(
            repository,
            new PasswordHasherStub(StoredHash, CorrectPassword),
            new AccessTokenIssuerStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new LoginCommand("member@example.test", CorrectPassword, null, null, null),
            CancellationToken.None);

        await Assert.ThrowsAsync<AccessDeniedException>(action);
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndRevokesPreviousSession()
    {
        var now = DateTimeOffset.UtcNow;
        var (repository, account, workspace) = await SeedLoggedInAccountAsync(now);
        var login = await Login(repository, account, workspace, now);
        var handler = new RefreshSessionHandler(
            repository, new AccessTokenIssuerStub(), new UnitOfWorkStub(), new FixedTimeProvider(now.AddMinutes(5)));

        var result = await handler.Handle(
            new RefreshSessionCommand(login.RefreshToken, null, "orbit-tests", "127.0.0.1"),
            CancellationToken.None);

        var initial = repository.Sessions.Single(session => session.Id == login.SessionId);
        Assert.Equal(RefreshSessionStatus.Rotated, initial.Status);
        Assert.Equal(2, repository.Sessions.Count);
        var rotated = repository.Sessions.Single(session => session.Id == result.SessionId);
        Assert.Equal(RefreshSessionStatus.Active, rotated.Status);
        Assert.Equal(initial.FamilyId, rotated.FamilyId);
        Assert.NotEqual(login.RefreshToken, result.RefreshToken);
    }

    [Fact]
    public async Task Refresh_SwitchesToRequestedActiveWorkspace()
    {
        var now = DateTimeOffset.UtcNow;
        var (repository, account, currentWorkspace) = await SeedLoggedInAccountAsync(now);
        var targetWorkspace = Workspace.Create(Guid.CreateVersion7(), "Target Workspace", now.AddMinutes(1));
        repository.Workspaces[targetWorkspace.Id] = targetWorkspace;
        repository.Memberships.Add(
            TenantMembership.CreateForUser(targetWorkspace.Id, account.Id, TenantRole.Administrator, now));
        var login = await Login(repository, account, currentWorkspace, now);
        var handler = new RefreshSessionHandler(
            repository, new AccessTokenIssuerStub(), new UnitOfWorkStub(), new FixedTimeProvider(now.AddMinutes(5)));

        var result = await handler.Handle(
            new RefreshSessionCommand(login.RefreshToken, targetWorkspace.Id, null, null),
            CancellationToken.None);

        Assert.Equal(targetWorkspace.Id, result.WorkspaceId);
        Assert.Equal(TenantRole.Administrator, result.Role);
        Assert.Equal(targetWorkspace.Id, repository.Sessions.Single(session => session.Id == result.SessionId).TenantId);
    }

    [Fact]
    public async Task Refresh_RejectsWorkspaceWithoutActiveMembershipBeforeRotatingToken()
    {
        var now = DateTimeOffset.UtcNow;
        var (repository, account, currentWorkspace) = await SeedLoggedInAccountAsync(now);
        var login = await Login(repository, account, currentWorkspace, now);
        var handler = new RefreshSessionHandler(
            repository, new AccessTokenIssuerStub(), new UnitOfWorkStub(), new FixedTimeProvider(now.AddMinutes(5)));

        var action = () => handler.Handle(
            new RefreshSessionCommand(login.RefreshToken, Guid.NewGuid(), null, null),
            CancellationToken.None);

        await Assert.ThrowsAsync<AccessDeniedException>(action);
        Assert.Equal(RefreshSessionStatus.Active, repository.Sessions.Single().Status);
    }

    [Fact]
    public async Task Refresh_ReuseOfRotatedToken_RevokesEntireFamily()
    {
        var now = DateTimeOffset.UtcNow;
        var (repository, account, workspace) = await SeedLoggedInAccountAsync(now);
        var login = await Login(repository, account, workspace, now);
        var tokenIssuer = new AccessTokenIssuerStub();
        var firstRefresh = await new RefreshSessionHandler(
                repository, tokenIssuer, new UnitOfWorkStub(), new FixedTimeProvider(now.AddMinutes(1)))
            .Handle(new RefreshSessionCommand(login.RefreshToken, null, null, null), CancellationToken.None);
        var handler = new RefreshSessionHandler(
            repository, tokenIssuer, new UnitOfWorkStub(), new FixedTimeProvider(now.AddMinutes(2)));

        // The original (already-rotated) token is replayed, as if it had been stolen.
        var action = () => handler.Handle(
            new RefreshSessionCommand(login.RefreshToken, null, null, null),
            CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(action);
        var rotatedSession = repository.Sessions.Single(session => session.Id == firstRefresh.SessionId);
        Assert.Equal(RefreshSessionStatus.Revoked, rotatedSession.Status);
    }

    [Fact]
    public async Task Refresh_ExpiredSession_Throws()
    {
        var now = DateTimeOffset.UtcNow;
        var (repository, account, workspace) = await SeedLoggedInAccountAsync(now);
        var login = await Login(repository, account, workspace, now.AddDays(-31));
        var handler = new RefreshSessionHandler(
            repository, new AccessTokenIssuerStub(), new UnitOfWorkStub(), new FixedTimeProvider(now));

        var action = () => handler.Handle(
            new RefreshSessionCommand(login.RefreshToken, null, null, null),
            CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(action);
    }

    [Fact]
    public async Task Logout_RevokesMatchingSession()
    {
        var now = DateTimeOffset.UtcNow;
        var (repository, account, workspace) = await SeedLoggedInAccountAsync(now);
        var login = await Login(repository, account, workspace, now);
        var handler = new LogoutHandler(repository, new UnitOfWorkStub(), TimeProvider.System);

        await handler.Handle(new LogoutCommand(login.RefreshToken), CancellationToken.None);

        var session = repository.Sessions.Single(session => session.Id == login.SessionId);
        Assert.Equal(RefreshSessionStatus.Revoked, session.Status);
    }

    [Fact]
    public async Task ListSessions_MarksCurrentSessionFromPrincipal()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var workspace = Workspace.Create(Guid.CreateVersion7(), "Team Workspace", now);
        var repository = new AuthRepositoryStub();
        repository.Workspaces[workspace.Id] = workspace;
        var current = RefreshSession.CreateInitial(
            userId, workspace.Id, "hash-current", "Chrome", "127.0.0.1", now, TimeSpan.FromDays(30));
        var other = RefreshSession.CreateInitial(
            userId, workspace.Id, "hash-other", "Firefox", "10.0.0.1", now, TimeSpan.FromDays(30));
        repository.Sessions.Add(current);
        repository.Sessions.Add(other);
        var handler = new ListSessionsHandler(new CurrentPrincipalStub(userId, current.Id), repository);

        var result = await handler.Handle(new ListSessionsQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.True(result.Single(session => session.SessionId == current.Id).IsCurrent);
        Assert.False(result.Single(session => session.SessionId == other.Id).IsCurrent);
    }

    [Fact]
    public async Task ListAccountWorkspaces_ReturnsOnlyCurrentUsersActiveMemberships()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var first = Workspace.Create(Guid.CreateVersion7(), "First Workspace", now.AddDays(-2));
        var second = Workspace.Create(Guid.CreateVersion7(), "Second Workspace", now.AddDays(-1));
        var repository = new AuthRepositoryStub();
        repository.Workspaces[first.Id] = first;
        repository.Workspaces[second.Id] = second;
        repository.Memberships.Add(
            TenantMembership.CreateForUser(first.Id, userId, TenantRole.Owner, now.AddDays(-2)));
        repository.Memberships.Add(
            TenantMembership.CreateForUser(second.Id, userId, TenantRole.Member, now.AddDays(-1)));
        repository.Memberships.Add(
            TenantMembership.CreateForUser(second.Id, otherUserId, TenantRole.Administrator, now));
        var handler = new ListAccountWorkspacesHandler(new CurrentPrincipalStub(userId, null), repository);

        var result = await handler.Handle(new ListAccountWorkspacesQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal([first.Id, second.Id], result.Select(workspace => workspace.Id));
        Assert.Equal(TenantRole.Owner, result[0].Role);
    }

    [Fact]
    public async Task RevokeSession_UnknownSession_ThrowsNotFound()
    {
        var repository = new AuthRepositoryStub();
        var handler = new RevokeSessionHandler(
            new CurrentPrincipalStub(Guid.NewGuid(), null), repository, new UnitOfWorkStub(), TimeProvider.System);

        var action = () => handler.Handle(new RevokeSessionCommand(Guid.NewGuid()), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    [Fact]
    public async Task RevokeOtherSessions_RevokesEveryoneExceptCurrent()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var repository = new AuthRepositoryStub();
        var current = RefreshSession.CreateInitial(userId, tenantId, "hash-current", null, null, now, TimeSpan.FromDays(30));
        var other1 = RefreshSession.CreateInitial(userId, tenantId, "hash-other-1", null, null, now, TimeSpan.FromDays(30));
        var other2 = RefreshSession.CreateInitial(userId, tenantId, "hash-other-2", null, null, now, TimeSpan.FromDays(30));
        repository.Sessions.Add(current);
        repository.Sessions.Add(other1);
        repository.Sessions.Add(other2);
        var unitOfWork = new UnitOfWorkStub();
        var handler = new RevokeOtherSessionsHandler(
            new CurrentPrincipalStub(userId, current.Id), repository, unitOfWork, TimeProvider.System);

        var revoked = await handler.Handle(new RevokeOtherSessionsCommand(), CancellationToken.None);

        Assert.Equal(2, revoked);
        Assert.Equal(RefreshSessionStatus.Active, current.Status);
        Assert.Equal(RefreshSessionStatus.Revoked, other1.Status);
        Assert.Equal(RefreshSessionStatus.Revoked, other2.Status);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private static async Task<(AuthRepositoryStub Repository, UserAccount Account, Workspace Workspace)>
        SeedLoggedInAccountAsync(DateTimeOffset now)
    {
        var account = UserAccount.Create("member@example.test", "Member One", now);
        var credential = LocalCredential.Create(account.Id, StoredHash, "Argon2id", 1, now);
        var workspace = Workspace.Create(Guid.CreateVersion7(), "Team Workspace", now);
        var repository = new AuthRepositoryStub { Account = account, Credential = credential };
        repository.Workspaces[workspace.Id] = workspace;
        repository.Memberships.Add(TenantMembership.CreateForUser(workspace.Id, account.Id, TenantRole.Member, now));
        return await Task.FromResult((repository, account, workspace));
    }

    private static Task<AuthSessionDto> Login(
        AuthRepositoryStub repository,
        UserAccount account,
        Workspace workspace,
        DateTimeOffset loginTime) =>
        new LoginHandler(
                repository,
                new PasswordHasherStub(StoredHash, CorrectPassword),
                new AccessTokenIssuerStub(),
                new UnitOfWorkStub(),
                new FixedTimeProvider(loginTime))
            .Handle(new LoginCommand(account.NormalizedEmail, CorrectPassword, workspace.Id, null, null), CancellationToken.None);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class PasswordHasherStub(string validEncodedHash, string matchingPassword) : IPasswordHasher
    {
        public int VerifyCount { get; private set; }

        public Task<PasswordHash> HashAsync(string password, CancellationToken cancellationToken) =>
            Task.FromResult(new PasswordHash(validEncodedHash, "Argon2id", 1));

        public Task<bool> VerifyAsync(string password, string? encodedHash, CancellationToken cancellationToken)
        {
            VerifyCount++;
            return Task.FromResult(encodedHash == validEncodedHash && password == matchingPassword);
        }
    }

    private sealed class AccessTokenIssuerStub : IAccessTokenIssuer
    {
        public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(30);

        public string LocalIssuer => "urn:orbit:local";

        public AccessToken IssueUserToken(Guid userId, Guid tenantId, Guid sessionId, DateTimeOffset now) =>
            new($"access-{sessionId}", now.AddMinutes(15));

        public AccessToken IssueServiceAccountToken(Guid tenantId, string clientId, DateTimeOffset now) =>
            new($"access-{clientId}", now.AddMinutes(15));
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class CurrentPrincipalStub(Guid userId, Guid? sessionId) : ICurrentPrincipal
    {
        public Guid? UserId => userId;
        public Guid? SessionId => sessionId;
        public Guid MembershipId => Guid.NewGuid();
        public PrincipalType PrincipalType => PrincipalType.User;
        public TenantRole TenantRole => TenantRole.Member;
        public bool IsDevelopmentBypass => false;
    }

    private sealed class AuthRepositoryStub : IAuthenticationRepository
    {
        public UserAccount? Account { get; set; }
        public LocalCredential? Credential { get; set; }
        public List<TenantMembership> Memberships { get; } = [];
        public Dictionary<Guid, Workspace> Workspaces { get; } = [];
        public List<RefreshSession> Sessions { get; } = [];

        public Task<UserAccount?> GetUserAccountAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Account?.Id == userId ? Account : null);

        public Task<UserAccount?> GetUserAccountByEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken) =>
            Task.FromResult(Account?.NormalizedEmail == normalizedEmail ? Account : null);

        public Task<LocalCredential?> GetLocalCredentialAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Credential?.UserId == userId ? Credential : null);

        public Task<IReadOnlyList<TenantMembership>> ListActiveMembershipsByUserAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>(
                Memberships.Where(membership => membership.UserId == userId).ToArray());

        public Task<Workspace?> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(Workspaces.GetValueOrDefault(tenantId));

        public Task<IReadOnlyList<Workspace>> GetWorkspacesAsync(
            IReadOnlyCollection<Guid> tenantIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Workspace>>(
                tenantIds
                    .Select(tenantId => Workspaces.GetValueOrDefault(tenantId))
                    .Where(workspace => workspace is not null)
                    .Select(workspace => workspace!)
                    .ToArray());

        public Task AddRefreshSessionAsync(RefreshSession session, CancellationToken cancellationToken)
        {
            Sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task<RefreshSession?> GetRefreshSessionByTokenHashAsync(
            string tokenHash,
            CancellationToken cancellationToken) =>
            Task.FromResult(Sessions.SingleOrDefault(session => session.TokenHash == tokenHash));

        public Task<RefreshSession?> GetActiveSessionAsync(
            Guid sessionId,
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Sessions.SingleOrDefault(session =>
                session.Id == sessionId
                && session.UserId == userId
                && session.Status == RefreshSessionStatus.Active));

        public Task<IReadOnlyList<RefreshSession>> ListActiveSessionsByUserAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RefreshSession>>(
                Sessions.Where(session => session.UserId == userId && session.Status == RefreshSessionStatus.Active)
                    .ToArray());

        public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
        {
            foreach (var session in Sessions.Where(
                session => session.FamilyId == familyId && session.Status == RefreshSessionStatus.Active))
            {
                session.Revoke(now);
            }

            return Task.CompletedTask;
        }

        public List<ExternalIdentity> ExternalIdentities { get; } = [];

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
    }
}
