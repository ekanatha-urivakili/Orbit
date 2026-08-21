using Orbit.Application.Abstractions;
using Orbit.Application.Access;
using Orbit.Application.Common;
using Orbit.Domain.Access;

namespace Orbit.Application.Tests;

public sealed class CreateTenantMembershipHandlerTests
{
    [Fact]
    public async Task Handle_PersistsAuthorizedServiceAccount()
    {
        var tenantId = Guid.NewGuid();
        var memberships = new MembershipRepositoryStub();
        var unitOfWork = new UnitOfWorkStub();
        var handler = new CreateTenantMembershipHandler(
            new TenantContextStub(tenantId),
            new AuthorizationStub(true),
            memberships,
            unitOfWork,
            TimeProvider.System);

        var result = await handler.Handle(
            new CreateTenantMembershipCommand(
                "https://identity.example.test",
                "automation-client",
                PrincipalType.ServiceAccount,
                TenantRole.Member),
            CancellationToken.None);

        Assert.Equal(PrincipalType.ServiceAccount, result.PrincipalType);
        Assert.Equal(tenantId, memberships.Added!.TenantId);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Handle_RejectsUnauthorizedRoleGrantBeforePersistence()
    {
        var memberships = new MembershipRepositoryStub();
        var handler = new CreateTenantMembershipHandler(
            new TenantContextStub(Guid.NewGuid()),
            new AuthorizationStub(false),
            memberships,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new CreateTenantMembershipCommand(
                "https://identity.example.test",
                "new-owner",
                PrincipalType.User,
                TenantRole.Owner),
            CancellationToken.None);

        await Assert.ThrowsAsync<AccessDeniedException>(action);
        Assert.Null(memberships.Added);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class AuthorizationStub(bool allowed) : ITenantAuthorization
    {
        public bool CanCreateProject() => allowed;
        public bool CanCreateMembership(TenantRole role) => allowed;
        public bool CanManageTeams() => allowed;
        public bool CanManageRoles() => allowed;
    }

    private sealed class MembershipRepositoryStub : ITenantMembershipRepository
    {
        public TenantMembership? Added { get; private set; }

        public Task AddAsync(TenantMembership membership, CancellationToken cancellationToken)
        {
            Added = membership;
            return Task.CompletedTask;
        }

        public Task<TenantMembership?> GetActiveAsync(
            Guid tenantId,
            string issuer,
            string subject,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<TenantMembership?> GetActiveByUserAsync(
            Guid tenantId,
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<TenantMembership?> GetActiveAsync(
            Guid tenantId,
            Guid membershipId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<TenantMembership?> GetOwnerAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<IReadOnlyList<TenantMembership>> ListAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<IReadOnlyList<TenantMembership>> ListByIdsAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> membershipIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<IReadOnlyList<Guid>> ListActiveUserIdsAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
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
}
