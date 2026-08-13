using Orbit.Application.Abstractions;
using Orbit.Application.Access;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Identity;
using Orbit.Domain.Settings;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Tests;

public sealed class TenantMembershipLifecycleHandlerTests
{
    [Fact]
    public async Task ChangeRole_PromotesMemberToAdministrator()
    {
        var workspace = Workspace.Create("Workspace", DateTimeOffset.UtcNow);
        var tenantId = workspace.Id;
        var membership = TenantMembership.CreateForUser(tenantId, Guid.NewGuid(), TenantRole.Member, DateTimeOffset.UtcNow);
        var memberships = new MembershipRepositoryStub([membership]);
        var handler = new ChangeTenantMembershipRoleHandler(
            new TenantContextStub(tenantId),
            new AuthorizationStub(true),
            new OwnerLockStub(),
            memberships,
            new SettingsRepositoryStub(workspace),
            new UnitOfWorkStub());

        var result = await handler.Handle(
            new ChangeTenantMembershipRoleCommand(membership.Id, TenantRole.Administrator),
            CancellationToken.None);

        Assert.Equal(TenantRole.Administrator, result.Role);
        Assert.Equal(2, workspace.AuthorizationEpoch);
    }

    [Fact]
    public async Task ChangeRole_RejectsDemotingTheSoleOwner()
    {
        var workspace = Workspace.Create("Workspace", DateTimeOffset.UtcNow);
        var tenantId = workspace.Id;
        var owner = TenantMembership.CreateForUser(tenantId, Guid.NewGuid(), TenantRole.Owner, DateTimeOffset.UtcNow);
        var memberships = new MembershipRepositoryStub([owner]);
        var handler = new ChangeTenantMembershipRoleHandler(
            new TenantContextStub(tenantId),
            new AuthorizationStub(true),
            new OwnerLockStub(),
            memberships,
            new SettingsRepositoryStub(workspace),
            new UnitOfWorkStub());

        var action = () => handler.Handle(
            new ChangeTenantMembershipRoleCommand(owner.Id, TenantRole.Administrator),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(action);
        Assert.Equal(TenantRole.Owner, owner.Role);
        Assert.Equal(1, workspace.AuthorizationEpoch);
    }

    [Fact]
    public async Task ChangeRole_AllowsDemotingAnOwnerWhenAnotherOwnerRemains()
    {
        var workspace = Workspace.Create("Workspace", DateTimeOffset.UtcNow);
        var tenantId = workspace.Id;
        var owner = TenantMembership.CreateForUser(tenantId, Guid.NewGuid(), TenantRole.Owner, DateTimeOffset.UtcNow);
        var coOwner = TenantMembership.CreateForUser(tenantId, Guid.NewGuid(), TenantRole.Owner, DateTimeOffset.UtcNow);
        var memberships = new MembershipRepositoryStub([owner, coOwner]);
        var handler = new ChangeTenantMembershipRoleHandler(
            new TenantContextStub(tenantId),
            new AuthorizationStub(true),
            new OwnerLockStub(),
            memberships,
            new SettingsRepositoryStub(workspace),
            new UnitOfWorkStub());

        var result = await handler.Handle(
            new ChangeTenantMembershipRoleCommand(owner.Id, TenantRole.Administrator),
            CancellationToken.None);

        Assert.Equal(TenantRole.Administrator, result.Role);
    }

    [Fact]
    public async Task ChangeRole_UnknownMembership_ThrowsNotFound()
    {
        var workspace = Workspace.Create("Workspace", DateTimeOffset.UtcNow);
        var handler = new ChangeTenantMembershipRoleHandler(
            new TenantContextStub(workspace.Id),
            new AuthorizationStub(true),
            new OwnerLockStub(),
            new MembershipRepositoryStub([]),
            new SettingsRepositoryStub(workspace),
            new UnitOfWorkStub());

        var action = () => handler.Handle(
            new ChangeTenantMembershipRoleCommand(Guid.NewGuid(), TenantRole.Administrator),
            CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    [Fact]
    public async Task Deactivate_RejectsRemovingTheSoleOwner()
    {
        var workspace = Workspace.Create("Workspace", DateTimeOffset.UtcNow);
        var tenantId = workspace.Id;
        var owner = TenantMembership.CreateForUser(tenantId, Guid.NewGuid(), TenantRole.Owner, DateTimeOffset.UtcNow);
        var memberships = new MembershipRepositoryStub([owner]);
        var handler = new DeactivateTenantMembershipHandler(
            new TenantContextStub(tenantId),
            new AuthorizationStub(true),
            new OwnerLockStub(),
            memberships,
            new SettingsRepositoryStub(workspace),
            new UnitOfWorkStub());

        var action = () => handler.Handle(new DeactivateTenantMembershipCommand(owner.Id), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(action);
        Assert.True(owner.IsActive);
        Assert.Equal(1, workspace.AuthorizationEpoch);
    }

    [Fact]
    public async Task Deactivate_RemovesAnOrdinaryMember()
    {
        var workspace = Workspace.Create("Workspace", DateTimeOffset.UtcNow);
        var tenantId = workspace.Id;
        var member = TenantMembership.CreateForUser(tenantId, Guid.NewGuid(), TenantRole.Member, DateTimeOffset.UtcNow);
        var memberships = new MembershipRepositoryStub([member]);
        var handler = new DeactivateTenantMembershipHandler(
            new TenantContextStub(tenantId),
            new AuthorizationStub(true),
            new OwnerLockStub(),
            memberships,
            new SettingsRepositoryStub(workspace),
            new UnitOfWorkStub());

        await handler.Handle(new DeactivateTenantMembershipCommand(member.Id), CancellationToken.None);

        Assert.False(member.IsActive);
        Assert.Equal(2, workspace.AuthorizationEpoch);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class OwnerLockStub : ITenantOwnerLock
    {
        public Task AcquireAsync(Guid tenantId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class AuthorizationStub(bool allowed) : ITenantAuthorization
    {
        public bool CanCreateProject() => allowed;
        public bool CanCreateMembership(TenantRole role) => allowed;
        public bool CanManageTeams() => allowed;
    }

    private sealed class MembershipRepositoryStub(List<TenantMembership> memberships) : ITenantMembershipRepository
    {
        public Task AddAsync(TenantMembership membership, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<TenantMembership?> GetActiveAsync(
            Guid tenantId, string issuer, string subject, CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<TenantMembership?> GetActiveByUserAsync(
            Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<TenantMembership?> GetActiveAsync(
            Guid tenantId, Guid membershipId, CancellationToken cancellationToken) =>
            Task.FromResult(memberships.SingleOrDefault(membership => membership.Id == membershipId && membership.IsActive));

        public Task<TenantMembership?> GetOwnerAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(memberships.SingleOrDefault(membership => membership.Role == TenantRole.Owner));

        public Task<IReadOnlyList<TenantMembership>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>(memberships);
    }

    private sealed class SettingsRepositoryStub(Workspace workspace) : ISettingsRepository
    {
        public Task<UserAccount?> GetUserAccountAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserAccount?>(null);

        public Task<UserPreference?> GetUserPreferenceAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserPreference?>(null);

        public Task<NotificationPreference?> GetNotificationPreferenceAsync(
            Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<NotificationPreference?>(null);

        public Task<Workspace?> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(workspace.Id == tenantId ? workspace : null);

        public Task<WorkspaceSetting?> GetWorkspaceSettingAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<WorkspaceSetting?>(null);

        public Task<ProjectSetting?> GetProjectSettingAsync(
            Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<ProjectSetting?>(null);

        public Task AddUserPreferenceAsync(UserPreference preference, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddNotificationPreferenceAsync(
            NotificationPreference preference, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddWorkspaceSettingAsync(WorkspaceSetting setting, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddProjectSettingAsync(ProjectSetting setting, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
