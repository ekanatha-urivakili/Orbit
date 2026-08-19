using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.Identity;
using Orbit.Domain.Access;
using Orbit.Domain.Identity;
using Orbit.Domain.Organizations;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Tests;

public sealed class BootstrapHandlerTests
{
    [Fact]
    public async Task Handle_CreatesAtomicBootstrapGraph()
    {
        var repository = new BootstrapRepositoryStub();
        var handler = new BootstrapHandler(repository, new PasswordHasherStub(), TimeProvider.System);

        var result = await handler.Handle(
            new BootstrapCommand(
                "First Admin",
                "ADMIN@Example.test",
                "StrongPassword123",
                "Orbit Team"),
            CancellationToken.None);

        Assert.Equal("admin@example.test", result.Email);
        Assert.Equal(result.UserId, repository.Account!.Id);
        Assert.Equal(result.UserId, repository.Credential!.UserId);
        Assert.Equal(result.UserId, repository.OwnerMembership!.UserId);
        Assert.Equal(result.WorkspaceId, repository.OwnerMembership.TenantId);
        Assert.DoesNotContain("StrongPassword123", repository.Credential.PasswordHash);
    }

    [Fact]
    public async Task Handle_RejectsReplayBeforeHashing()
    {
        var passwordHasher = new PasswordHasherStub();
        var handler = new BootstrapHandler(
            new BootstrapRepositoryStub { InitializationRequired = false },
            passwordHasher,
            TimeProvider.System);

        var action = () => handler.Handle(
            new BootstrapCommand(
                "First Admin",
                "admin@example.test",
                "StrongPassword123",
                "Orbit Team"),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(action);
        Assert.Equal(0, passwordHasher.HashCount);
    }

    private sealed class PasswordHasherStub : IPasswordHasher
    {
        public int HashCount { get; private set; }

        public Task<PasswordHash> HashAsync(string password, CancellationToken cancellationToken)
        {
            HashCount++;
            return Task.FromResult(new PasswordHash("hashed-value", "Argon2id", 1));
        }

        public Task<bool> VerifyAsync(string password, string? encodedHash, CancellationToken cancellationToken) =>
            Task.FromResult(encodedHash == "hashed-value");
    }

    private sealed class BootstrapRepositoryStub : IBootstrapRepository
    {
        public bool InitializationRequired { get; init; } = true;
        public UserAccount? Account { get; private set; }
        public LocalCredential? Credential { get; private set; }
        public Organization? Organization { get; private set; }
        public OrganizationMembership? OrganizationMembership { get; private set; }
        public TenantMembership? OwnerMembership { get; private set; }

        public Task<bool> IsInitializationRequiredAsync(CancellationToken cancellationToken) =>
            Task.FromResult(InitializationRequired);

        public Task<bool> TryInitializeAsync(
            UserAccount account,
            LocalCredential credential,
            SiteRoleAssignment siteRole,
            Organization organization,
            Workspace workspace,
            OrganizationMembership organizationMembership,
            TenantMembership ownerMembership,
            CancellationToken cancellationToken)
        {
            Account = account;
            Credential = credential;
            Organization = organization;
            OrganizationMembership = organizationMembership;
            OwnerMembership = ownerMembership;
            return Task.FromResult(InitializationRequired);
        }
    }
}
