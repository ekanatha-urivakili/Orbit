using System.Security.Cryptography;
using System.Text;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.Identity;
using Orbit.Domain.Access;
using Orbit.Domain.Identity;
using Orbit.Domain.Messaging;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Tests;

public sealed class PasswordResetHandlerTests
{
    [Fact]
    public async Task Request_EnqueuesOneEmailAndCreatesATokenForAKnownAccount()
    {
        var account = UserAccount.Create("user@example.test", "Test User", DateTimeOffset.UtcNow);
        var credential = LocalCredential.Create(account.Id, "hash-1", "argon2id", 1, DateTimeOffset.UtcNow);
        var repository = new AuthRepositoryStub([account], [credential]);
        var outbox = new OutboxRepositoryStub();
        var handler = new RequestPasswordResetHandler(repository, outbox, new UnitOfWorkStub(), TimeProvider.System);

        await handler.Handle(
            new RequestPasswordResetCommand(account.NormalizedEmail, "http://localhost:5173"),
            CancellationToken.None);

        Assert.Single(outbox.Messages);
        Assert.Equal(account.NormalizedEmail, outbox.Messages[0].ToEmail);
        Assert.Contains("#resetToken=", outbox.Messages[0].HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("?resetToken=", outbox.Messages[0].HtmlBody, StringComparison.Ordinal);
        Assert.Single(repository.PasswordResetTokens);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://orbit.example.test/?redirect=attacker")]
    [InlineData("https://orbit.example.test/#attacker")]
    public void RequestValidator_RejectsUnsafeFrontendBaseUrl(string frontendBaseUrl)
    {
        var validator = new RequestPasswordResetValidator();

        var result = validator.Validate(new RequestPasswordResetCommand("user@example.test", frontendBaseUrl));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Request_IsANoOpForAnUnknownEmail()
    {
        var repository = new AuthRepositoryStub([], []);
        var outbox = new OutboxRepositoryStub();
        var handler = new RequestPasswordResetHandler(repository, outbox, new UnitOfWorkStub(), TimeProvider.System);

        await handler.Handle(
            new RequestPasswordResetCommand("nobody@example.test", "http://localhost:5173"),
            CancellationToken.None);

        Assert.Empty(outbox.Messages);
        Assert.Empty(repository.PasswordResetTokens);
    }

    [Fact]
    public async Task Request_IsANoOpForAnAccountWithoutALocalCredential()
    {
        var account = UserAccount.Create("user@example.test", "Test User", DateTimeOffset.UtcNow);
        var repository = new AuthRepositoryStub([account], []);
        var outbox = new OutboxRepositoryStub();
        var handler = new RequestPasswordResetHandler(repository, outbox, new UnitOfWorkStub(), TimeProvider.System);

        await handler.Handle(
            new RequestPasswordResetCommand(account.NormalizedEmail, "http://localhost:5173"),
            CancellationToken.None);

        Assert.Empty(outbox.Messages);
        Assert.Empty(repository.PasswordResetTokens);
    }

    [Fact]
    public async Task Confirm_UpdatesPasswordAndRevokesAllSessions()
    {
        var now = DateTimeOffset.UtcNow;
        var account = UserAccount.Create("user@example.test", "Test User", now);
        var credential = LocalCredential.Create(account.Id, "old-hash", "argon2id", 1, now);
        var session = RefreshSession.CreateInitial(
            account.Id, Guid.NewGuid(), "session-hash", null, null, false, now, TimeSpan.FromDays(30));
        var repository = new AuthRepositoryStub([account], [credential], sessions: [session]);
        var outbox = new OutboxRepositoryStub();
        var requestHandler = new RequestPasswordResetHandler(
            repository, outbox, new UnitOfWorkStub(), TimeProvider.System);
        await requestHandler.Handle(
            new RequestPasswordResetCommand(account.NormalizedEmail, "http://localhost:5173"),
            CancellationToken.None);
        var rawToken = ExtractResetToken(outbox.Messages[0].HtmlBody);

        var confirmHandler = new ConfirmPasswordResetHandler(
            repository, new PasswordHasherStub(), new UnitOfWorkStub(), TimeProvider.System);
        await confirmHandler.Handle(new ConfirmPasswordResetCommand(rawToken, "NewPassword123"), CancellationToken.None);

        Assert.Equal(PasswordResetTokenStatus.Used, repository.PasswordResetTokens[0].Status);
        Assert.Equal("new-hash", credential.PasswordHash);
        Assert.Equal(RefreshSessionStatus.Revoked, session.Status);
    }

    [Fact]
    public async Task Confirm_ThrowsForAnUnknownToken()
    {
        var repository = new AuthRepositoryStub([], []);
        var handler = new ConfirmPasswordResetHandler(
            repository, new PasswordHasherStub(), new UnitOfWorkStub(), TimeProvider.System);

        var action = () => handler.Handle(
            new ConfirmPasswordResetCommand("raw-token", "NewPassword123"), CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(action);
    }

    [Fact]
    public async Task Confirm_ThrowsForAnExpiredToken()
    {
        var now = DateTimeOffset.UtcNow;
        var account = UserAccount.Create("user@example.test", "Test User", now);
        var credential = LocalCredential.Create(account.Id, "old-hash", "argon2id", 1, now);
        var token = PasswordResetToken.Create(account.Id, Hash("raw-token"), now.AddHours(-2), TimeSpan.FromHours(1));
        var repository = new AuthRepositoryStub([account], [credential], [token]);
        var handler = new ConfirmPasswordResetHandler(
            repository, new PasswordHasherStub(), new UnitOfWorkStub(), TimeProvider.System);

        var action = () => handler.Handle(
            new ConfirmPasswordResetCommand("raw-token", "NewPassword123"), CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(action);
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string ExtractResetToken(string htmlBody)
    {
        const string marker = "#resetToken=";
        var start = htmlBody.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = htmlBody.IndexOf('"', start);
        return Uri.UnescapeDataString(htmlBody[start..end]);
    }

    private sealed class PasswordHasherStub : IPasswordHasher
    {
        public Task<PasswordHash> HashAsync(string password, CancellationToken cancellationToken) =>
            Task.FromResult(new PasswordHash("new-hash", "argon2id", 1));

        public Task<bool> VerifyAsync(string password, string? encodedHash, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class OutboxRepositoryStub : IOutboxRepository
    {
        public List<OutboxEmailMessage> Messages { get; } = [];

        public Task AddAsync(OutboxEmailMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private sealed class AuthRepositoryStub(
        List<UserAccount> accounts,
        List<LocalCredential> credentials,
        List<PasswordResetToken>? passwordResetTokens = null,
        List<RefreshSession>? sessions = null) : IAuthenticationRepository
    {
        public List<GoogleSignInHandoff> SignInHandoffs { get; } = [];

        public List<PasswordResetToken> PasswordResetTokens { get; } = passwordResetTokens ?? [];

        private readonly List<RefreshSession> _sessions = sessions ?? [];

        public Task<UserAccount?> GetUserAccountAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(accounts.SingleOrDefault(account => account.Id == userId));

        public Task<UserAccount?> GetUserAccountByEmailAsync(
            string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(accounts.SingleOrDefault(account => account.NormalizedEmail == normalizedEmail));

        public Task<LocalCredential?> GetLocalCredentialAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(credentials.SingleOrDefault(credential => credential.UserId == userId));

        public Task<IReadOnlyList<TenantMembership>> ListActiveMembershipsByUserAsync(
            Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<Workspace?> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<Workspace?>(null);

        public Task<IReadOnlyList<Workspace>> GetWorkspacesAsync(
            IReadOnlyCollection<Guid> tenantIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Workspace>>([]);

        public Task AddRefreshSessionAsync(RefreshSession session, CancellationToken cancellationToken)
        {
            _sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task<RefreshSession?> GetRefreshSessionByTokenHashAsync(
            string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult<RefreshSession?>(null);

        public Task<RefreshSession?> GetActiveSessionAsync(
            Guid sessionId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<RefreshSession?>(null);

        public Task<IReadOnlyList<RefreshSession>> ListActiveSessionsByUserAsync(
            Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RefreshSession>>(
                _sessions.Where(session => session.UserId == userId
                    && session.Status == RefreshSessionStatus.Active).ToArray());

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

        public Task<ExternalIdentity?> GetExternalIdentityAsync(
            Guid id, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<ExternalIdentity?>(null);

        public Task RemoveExternalIdentityAsync(ExternalIdentity identity, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddPasswordResetTokenAsync(PasswordResetToken token, CancellationToken cancellationToken)
        {
            PasswordResetTokens.Add(token);
            return Task.CompletedTask;
        }

        public Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(
            string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult(PasswordResetTokens.SingleOrDefault(token => token.TokenHash == tokenHash));

        public Task RevokeActivePasswordResetTokensForUserAsync(
            Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
        {
            foreach (var token in PasswordResetTokens.Where(token =>
                token.UserId == userId && token.Status == PasswordResetTokenStatus.Active))
            {
                token.Revoke(now);
            }

            return Task.CompletedTask;
        }

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
}
