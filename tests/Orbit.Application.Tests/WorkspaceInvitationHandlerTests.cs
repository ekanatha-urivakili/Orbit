using System.Security.Cryptography;
using System.Text;
using Orbit.Application.Abstractions;
using Orbit.Application.Access;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Directory;
using Orbit.Domain.Identity;

namespace Orbit.Application.Tests;

public sealed class WorkspaceInvitationHandlerTests
{
    [Fact]
    public async Task Accept_CreatesAccountAndMembershipAndConsumesInvitation()
    {
        var tenantId = Guid.NewGuid();
        var invitation = CreateInvitation(tenantId, "raw-token");
        var repository = new InvitationRepositoryStub(invitation);
        var handler = CreateHandler(tenantId, repository, passwordValid: true);

        var result = await handler.Handle(
            new AcceptWorkspaceInvitationCommand("raw-token", "Invited User", "StrongPassword123"),
            CancellationToken.None);

        Assert.NotNull(repository.AddedAccount);
        Assert.NotNull(repository.AddedCredential);
        Assert.Equal(repository.AddedAccount!.Id, result.UserId);
        Assert.Equal(WorkspaceInvitationStatus.Accepted, invitation.Status);
    }

    [Fact]
    public async Task Accept_RejectsTokenFromAnotherWorkspace()
    {
        var invitation = CreateInvitation(Guid.NewGuid(), "raw-token");
        var repository = new InvitationRepositoryStub(invitation);
        var handler = CreateHandler(Guid.NewGuid(), repository, passwordValid: true);

        var action = () => handler.Handle(
            new AcceptWorkspaceInvitationCommand("raw-token", "Invited User", "StrongPassword123"),
            CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(action);
        Assert.Equal(WorkspaceInvitationStatus.Active, invitation.Status);
    }

    [Fact]
    public async Task Accept_RejectsReplay()
    {
        var tenantId = Guid.NewGuid();
        var invitation = CreateInvitation(tenantId, "raw-token");
        var repository = new InvitationRepositoryStub(invitation);
        var handler = CreateHandler(tenantId, repository, passwordValid: true);
        var command = new AcceptWorkspaceInvitationCommand("raw-token", "Invited User", "StrongPassword123");
        await handler.Handle(command, CancellationToken.None);

        var action = () => handler.Handle(command, CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(action);
    }

    [Fact]
    public async Task Accept_ExistingAccountRequiresValidPassword()
    {
        var tenantId = Guid.NewGuid();
        var invitation = CreateInvitation(tenantId, "raw-token");
        var account = UserAccount.Create("invited@example.test", "Existing User", DateTimeOffset.UtcNow);
        var repository = new InvitationRepositoryStub(invitation, account);
        var handler = CreateHandler(tenantId, repository, passwordValid: false);

        var action = () => handler.Handle(
            new AcceptWorkspaceInvitationCommand("raw-token", "Existing User", "WrongPassword123"),
            CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(action);
        Assert.Equal(WorkspaceInvitationStatus.Active, invitation.Status);
    }

    [Fact]
    public async Task Accept_UpgradesRoleForExistingActiveMember()
    {
        var tenantId = Guid.NewGuid();
        var invitation = CreateInvitation(tenantId, "raw-token", TenantRole.Administrator);
        var account = UserAccount.Create("invited@example.test", "Existing User", DateTimeOffset.UtcNow);
        var membership = TenantMembership.CreateForUser(tenantId, account.Id, TenantRole.Member, DateTimeOffset.UtcNow);
        var repository = new InvitationRepositoryStub(invitation, account, membership);
        var handler = CreateHandler(tenantId, repository, passwordValid: true);

        var result = await handler.Handle(
            new AcceptWorkspaceInvitationCommand("raw-token", "Existing User", "StrongPassword123"),
            CancellationToken.None);

        Assert.Equal(TenantRole.Administrator, membership.Role);
        Assert.Equal(TenantRole.Administrator, result.Role);
    }

    [Fact]
    public async Task List_ForwardsNormalizedEmailAndStatusFilters()
    {
        var tenantId = Guid.NewGuid();
        var repository = new ListRepositorySpy();
        var handler = new ListWorkspaceInvitationsHandler(
            new TenantContextStub(tenantId),
            new AuthorizationStub(true),
            repository);

        await handler.Handle(
            new ListWorkspaceInvitationsQuery(" Alice@Example.TEST ", WorkspaceInvitationStatus.Revoked),
            CancellationToken.None);

        Assert.Equal(tenantId, repository.LastTenantId);
        Assert.Equal("alice@example.test", repository.LastEmailSearch);
        Assert.Equal(WorkspaceInvitationStatus.Revoked, repository.LastStatus);
    }

    private sealed class AuthorizationStub(bool allowed) : ITenantAuthorization
    {
        public bool CanCreateProject() => allowed;
        public bool CanCreateMembership(TenantRole role) => allowed;
        public bool CanManageTeams() => allowed;
    }

    private sealed class ListRepositorySpy : IWorkspaceInvitationRepository
    {
        public Guid LastTenantId { get; private set; }
        public string? LastEmailSearch { get; private set; }
        public WorkspaceInvitationStatus? LastStatus { get; private set; }

        public Task AddAsync(WorkspaceInvitation value, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<WorkspaceInvitation?> GetActiveByEmailAsync(
            Guid tenantId,
            string normalizedEmail,
            CancellationToken cancellationToken) =>
            Task.FromResult<WorkspaceInvitation?>(null);

        public Task<WorkspaceInvitation?> GetByTokenHashAsync(
            Guid tenantId,
            string tokenHash,
            CancellationToken cancellationToken) =>
            Task.FromResult<WorkspaceInvitation?>(null);

        public Task<WorkspaceInvitation?> GetAsync(
            Guid tenantId,
            Guid invitationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<WorkspaceInvitation?>(null);

        public Task<IReadOnlyList<WorkspaceInvitation>> ListAsync(
            Guid tenantId,
            string? emailSearch,
            WorkspaceInvitationStatus? status,
            CancellationToken cancellationToken)
        {
            LastTenantId = tenantId;
            LastEmailSearch = emailSearch;
            LastStatus = status;
            return Task.FromResult<IReadOnlyList<WorkspaceInvitation>>([]);
        }

        public Task<UserAccount?> GetUserAccountByEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken) =>
            Task.FromResult<UserAccount?>(null);

        public Task<LocalCredential?> GetUserAccountCredentialAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<LocalCredential?>(null);

        public Task<TenantMembership?> GetMembershipByUserAsync(
            Guid tenantId,
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<TeamMembership?> GetTeamMembershipAsync(
            Guid tenantId,
            Guid teamId,
            Guid membershipId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TeamMembership?>(null);

        public Task AddUserAccountAsync(UserAccount account, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddLocalCredentialAsync(LocalCredential credential, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddTenantMembershipAsync(TenantMembership membership, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddTeamMembershipAsync(TeamMembership membership, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    [Fact]
    public async Task AcceptExternal_CreatesFederatedAccountWithoutPassword()
    {
        var tenantId = Guid.NewGuid();
        var invitation = CreateInvitation(tenantId, "raw-token");
        var invitationRepository = new InvitationRepositoryStub(invitation);
        var authRepository = new ExternalAuthRepositoryStub();
        var handler = new AcceptWorkspaceInvitationWithExternalIdentityHandler(
            new TenantContextStub(tenantId),
            invitationRepository,
            authRepository,
            new ExternalTokenValidatorStub("invited@example.test", emailVerified: true),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new AcceptWorkspaceInvitationWithExternalIdentityCommand("raw-token", "external-proof", "Invited User"),
            CancellationToken.None);

        Assert.NotNull(invitationRepository.AddedAccount);
        Assert.Null(invitationRepository.AddedCredential);
        Assert.Single(authRepository.AddedIdentities);
        Assert.Equal(invitationRepository.AddedAccount!.Id, authRepository.AddedIdentities[0].UserId);
        Assert.Equal(invitationRepository.AddedAccount!.Id, result.UserId);
        Assert.Equal(WorkspaceInvitationStatus.Accepted, invitation.Status);
    }

    [Fact]
    public async Task AcceptExternal_RejectsUnverifiedEmail()
    {
        var tenantId = Guid.NewGuid();
        var invitation = CreateInvitation(tenantId, "raw-token");
        var handler = new AcceptWorkspaceInvitationWithExternalIdentityHandler(
            new TenantContextStub(tenantId),
            new InvitationRepositoryStub(invitation),
            new ExternalAuthRepositoryStub(),
            new ExternalTokenValidatorStub("invited@example.test", emailVerified: false),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new AcceptWorkspaceInvitationWithExternalIdentityCommand("raw-token", "external-proof", "Invited User"),
            CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(action);
        Assert.Equal(WorkspaceInvitationStatus.Active, invitation.Status);
    }

    [Fact]
    public async Task AcceptExternal_RejectsMismatchedEmail()
    {
        var tenantId = Guid.NewGuid();
        var invitation = CreateInvitation(tenantId, "raw-token");
        var handler = new AcceptWorkspaceInvitationWithExternalIdentityHandler(
            new TenantContextStub(tenantId),
            new InvitationRepositoryStub(invitation),
            new ExternalAuthRepositoryStub(),
            new ExternalTokenValidatorStub("someone-else@example.test", emailVerified: true),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new AcceptWorkspaceInvitationWithExternalIdentityCommand("raw-token", "external-proof", "Invited User"),
            CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(action);
    }

    [Fact]
    public async Task AcceptExternal_ReusesExistingLinkedAccount()
    {
        var tenantId = Guid.NewGuid();
        var invitation = CreateInvitation(tenantId, "raw-token");
        var account = UserAccount.Create("invited@example.test", "Existing User", DateTimeOffset.UtcNow);
        var identity = ExternalIdentity.Create(account.Id, "https://idp.example.test", "subject-1", DateTimeOffset.UtcNow);
        var authRepository = new ExternalAuthRepositoryStub(account, identity);
        var handler = new AcceptWorkspaceInvitationWithExternalIdentityHandler(
            new TenantContextStub(tenantId),
            new InvitationRepositoryStub(invitation),
            authRepository,
            new ExternalTokenValidatorStub("invited@example.test", emailVerified: true),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new AcceptWorkspaceInvitationWithExternalIdentityCommand("raw-token", "external-proof", "Existing User"),
            CancellationToken.None);

        Assert.Equal(account.Id, result.UserId);
        Assert.Empty(authRepository.AddedIdentities);
    }

    private sealed class ExternalTokenValidatorStub(string email, bool emailVerified) : IExternalIdentityTokenValidator
    {
        public Task<VerifiedExternalIdentity> ValidateAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult(new VerifiedExternalIdentity("https://idp.example.test", "subject-1", email, emailVerified));
    }

    private sealed class ExternalAuthRepositoryStub(UserAccount? account = null, ExternalIdentity? identity = null)
        : IAuthenticationRepository
    {
        public List<GoogleSignInHandoff> SignInHandoffs { get; } = [];

        public List<ExternalIdentity> AddedIdentities { get; } = [];

        public Task<UserAccount?> GetUserAccountAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(account?.Id == userId ? account : null);

        public Task<UserAccount?> GetUserAccountByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult<UserAccount?>(null);

        public Task<LocalCredential?> GetLocalCredentialAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<LocalCredential?>(null);

        public Task<IReadOnlyList<TenantMembership>> ListActiveMembershipsByUserAsync(
            Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<Domain.Workspaces.Workspace?> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<Domain.Workspaces.Workspace?>(null);

        public Task<IReadOnlyList<Domain.Workspaces.Workspace>> GetWorkspacesAsync(
            IReadOnlyCollection<Guid> tenantIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Domain.Workspaces.Workspace>>([]);

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

        public Task AddExternalIdentityAsync(ExternalIdentity value, CancellationToken cancellationToken)
        {
            AddedIdentities.Add(value);
            return Task.CompletedTask;
        }

        public Task<ExternalIdentity?> GetExternalIdentityAsync(string issuer, string subject, CancellationToken cancellationToken) =>
            Task.FromResult(identity is not null && identity.Issuer == issuer && identity.Subject == subject
                ? identity
                : null);

        public Task<IReadOnlyList<ExternalIdentity>> ListExternalIdentitiesByUserAsync(
            Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExternalIdentity>>([]);

        public Task<ExternalIdentity?> GetExternalIdentityAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<ExternalIdentity?>(null);

        public Task RemoveExternalIdentityAsync(ExternalIdentity value, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddPasswordResetTokenAsync(PasswordResetToken token, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult<PasswordResetToken?>(null);

        public Task RevokeActivePasswordResetTokensForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
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

    private static AcceptWorkspaceInvitationHandler CreateHandler(
        Guid tenantId,
        InvitationRepositoryStub repository,
        bool passwordValid) =>
        new(
            new TenantContextStub(tenantId),
            repository,
            new PasswordHasherStub(passwordValid),
            new UnitOfWorkStub(),
            TimeProvider.System);

    private static WorkspaceInvitation CreateInvitation(
        Guid tenantId,
        string rawToken,
        TenantRole role = TenantRole.Member) =>
        WorkspaceInvitation.Create(
            tenantId,
            "invited@example.test",
            role,
            null,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            TimeSpan.FromDays(7));

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class PasswordHasherStub(bool passwordValid) : IPasswordHasher
    {
        public Task<PasswordHash> HashAsync(string password, CancellationToken cancellationToken) =>
            Task.FromResult(new PasswordHash("new-hash", "argon2id", 1));

        public Task<bool> VerifyAsync(string password, string? encodedHash, CancellationToken cancellationToken) =>
            Task.FromResult(passwordValid);
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private sealed class InvitationRepositoryStub(
        WorkspaceInvitation invitation,
        UserAccount? existingAccount = null,
        TenantMembership? existingMembership = null) : IWorkspaceInvitationRepository
    {
        public UserAccount? AddedAccount { get; private set; }
        public LocalCredential? AddedCredential { get; private set; }
        private TenantMembership? _membership = existingMembership;

        public Task AddAsync(WorkspaceInvitation value, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<WorkspaceInvitation?> GetActiveByEmailAsync(
            Guid tenantId,
            string normalizedEmail,
            CancellationToken cancellationToken) =>
            Task.FromResult<WorkspaceInvitation?>(null);

        public Task<WorkspaceInvitation?> GetByTokenHashAsync(
            Guid tenantId,
            string tokenHash,
            CancellationToken cancellationToken) =>
            Task.FromResult(invitation.TenantId == tenantId && invitation.TokenHash == tokenHash ? invitation : null);

        public Task<WorkspaceInvitation?> GetAsync(
            Guid tenantId,
            Guid invitationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<WorkspaceInvitation?>(null);

        public Task<IReadOnlyList<WorkspaceInvitation>> ListAsync(
            Guid tenantId,
            string? emailSearch,
            WorkspaceInvitationStatus? status,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkspaceInvitation>>([]);

        public Task<UserAccount?> GetUserAccountByEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken) =>
            Task.FromResult(existingAccount ?? AddedAccount);

        public Task<LocalCredential?> GetUserAccountCredentialAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult(existingAccount is null
                ? AddedCredential
                : LocalCredential.Create(userId, "old-hash", "argon2id", 1, DateTimeOffset.UtcNow));

        public Task<TenantMembership?> GetMembershipByUserAsync(
            Guid tenantId,
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult(_membership);

        public Task<TeamMembership?> GetTeamMembershipAsync(
            Guid tenantId,
            Guid teamId,
            Guid membershipId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TeamMembership?>(null);

        public Task AddUserAccountAsync(UserAccount account, CancellationToken cancellationToken)
        {
            AddedAccount = account;
            return Task.CompletedTask;
        }

        public Task AddLocalCredentialAsync(LocalCredential credential, CancellationToken cancellationToken)
        {
            AddedCredential = credential;
            return Task.CompletedTask;
        }

        public Task AddTenantMembershipAsync(TenantMembership membership, CancellationToken cancellationToken)
        {
            _membership = membership;
            return Task.CompletedTask;
        }

        public Task AddTeamMembershipAsync(TeamMembership membership, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
