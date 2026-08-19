using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.Identity;
using Orbit.Application.Organizations;
using Orbit.Domain.Access;
using Orbit.Domain.Identity;
using Orbit.Domain.Organizations;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Tests;

public sealed class SignUpHandlerTests
{
    [Fact]
    public async Task Handle_CreatesOrganizationWorkspaceAndSession()
    {
        var repository = new SignUpRepositoryStub();
        var handler = new SignUpHandler(
            repository,
            new PasswordHasherStub(),
            new AccessTokenIssuerStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new SignUpCommand(
                "New Owner",
                "owner@example.test",
                "StrongPassword123",
                "Acme Inc",
                "Acme Workspace",
                "orbit-tests",
                "127.0.0.1"),
            CancellationToken.None);

        Assert.Equal("owner@example.test", result.Email);
        Assert.Equal(TenantRole.Owner, result.Role);
        Assert.Equal(result.UserId, repository.Account!.Id);
        Assert.Equal(result.UserId, repository.OrganizationMembership!.UserId);
        Assert.Equal(OrganizationRole.Owner, repository.OrganizationMembership.Role);
        Assert.Equal(repository.Organization!.Id, repository.Workspace!.OrganizationId);
        Assert.Equal(result.WorkspaceId, repository.Workspace.Id);
        Assert.NotEmpty(result.RefreshToken);
        Assert.NotEmpty(result.AccessToken);
        Assert.DoesNotContain("StrongPassword123", repository.Credential!.PasswordHash);
    }

    [Fact]
    public async Task Handle_RejectsDuplicateEmail()
    {
        var repository = new SignUpRepositoryStub { EmailExists = true };
        var handler = new SignUpHandler(
            repository,
            new PasswordHasherStub(),
            new AccessTokenIssuerStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new SignUpCommand(
                "New Owner",
                "owner@example.test",
                "StrongPassword123",
                "Acme Inc",
                "Acme Workspace",
                null,
                null),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(action);
        Assert.Null(repository.Account);
    }

    private sealed class PasswordHasherStub : IPasswordHasher
    {
        public Task<PasswordHash> HashAsync(string password, CancellationToken cancellationToken) =>
            Task.FromResult(new PasswordHash("hashed-value", "Argon2id", 1));

        public Task<bool> VerifyAsync(string password, string? encodedHash, CancellationToken cancellationToken) =>
            Task.FromResult(encodedHash == "hashed-value");
    }

    private sealed class AccessTokenIssuerStub : IAccessTokenIssuer
    {
        public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(30);

        public TimeSpan PersistentRefreshTokenLifetime => TimeSpan.FromDays(30);

        public string LocalIssuer => "urn:orbit:local";

        public AccessToken IssueUserToken(Guid userId, Guid tenantId, Guid sessionId, DateTimeOffset now) =>
            new($"access-{sessionId}", now.AddMinutes(15));

        public AccessToken IssueServiceAccountToken(Guid tenantId, string clientId, DateTimeOffset now) =>
            new($"access-{clientId}", now.AddMinutes(15));
    }

    private sealed class SignUpRepositoryStub : ISignUpRepository
    {
        public bool EmailExists { get; init; }
        public UserAccount? Account { get; private set; }
        public LocalCredential? Credential { get; private set; }
        public Organization? Organization { get; private set; }
        public Workspace? Workspace { get; private set; }
        public OrganizationMembership? OrganizationMembership { get; private set; }
        public TenantMembership? OwnerMembership { get; private set; }
        public RefreshSession? RefreshSession { get; private set; }

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(EmailExists);

        public Task AddAsync(
            UserAccount account,
            LocalCredential credential,
            Organization organization,
            Workspace workspace,
            OrganizationMembership organizationMembership,
            TenantMembership ownerMembership,
            RefreshSession refreshSession,
            CancellationToken cancellationToken)
        {
            Account = account;
            Credential = credential;
            Organization = organization;
            Workspace = workspace;
            OrganizationMembership = organizationMembership;
            OwnerMembership = ownerMembership;
            RefreshSession = refreshSession;
            return Task.CompletedTask;
        }

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
}
