using Orbit.Application.Abstractions;
using Orbit.Application.Access;
using Orbit.Application.Common;
using Orbit.Application.Directory;
using Orbit.Domain.Access;
using Orbit.Domain.Directory;
using Orbit.Domain.Identity;
using Orbit.Domain.Projects;
using Orbit.Domain.Settings;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Tests;

public sealed class GroupHandlerTests
{
    [Fact]
    public async Task CreateGroup_PersistsGroupOwnedByCurrentTenant()
    {
        var tenantId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var groups = new DirectoryGroupRepositoryStub();
        var handler = new CreateGroupHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(membershipId),
            new AuthorizationStub(true),
            groups,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(new CreateGroupCommand("Platform Group"), CancellationToken.None);

        Assert.Equal("Platform Group", result.Name);
        Assert.Equal(tenantId, groups.Added!.TenantId);
        Assert.Equal(membershipId, groups.Added!.CreatedByMembershipId);
    }

    [Fact]
    public async Task CreateGroup_RejectsUnauthorizedPrincipal()
    {
        var groups = new DirectoryGroupRepositoryStub();
        var handler = new CreateGroupHandler(
            new TenantContextStub(Guid.NewGuid()),
            new CurrentPrincipalStub(Guid.NewGuid()),
            new AuthorizationStub(false),
            groups,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(new CreateGroupCommand("Platform Group"), CancellationToken.None);

        await Assert.ThrowsAsync<AccessDeniedException>(action);
        Assert.Null(groups.Added);
    }

    [Fact]
    public async Task AddGroupMember_RejectsDuplicateMembership()
    {
        var workspace = Workspace.Create(Guid.CreateVersion7(), "Workspace", DateTimeOffset.UtcNow);
        var tenantId = workspace.Id;
        var group = DirectoryGroup.Create(tenantId, "Platform Group", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var membershipId = Guid.NewGuid();
        var groups = new DirectoryGroupRepositoryStub { Existing = group };
        var groupMemberships = new GroupMembershipRepositoryStub
        {
            Existing = GroupMembership.Create(tenantId, group.Id, membershipId, DateTimeOffset.UtcNow)
        };
        var handler = new AddGroupMemberHandler(
            new TenantContextStub(tenantId),
            new AuthorizationStub(true),
            groups,
            groupMemberships,
            new TenantMembershipLookupStub(membershipId),
            new SettingsRepositoryStub(workspace),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(new AddGroupMemberCommand(group.Id, membershipId), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(action);
    }

    [Fact]
    public async Task AddGroupMember_PersistsNewMembership()
    {
        var workspace = Workspace.Create(Guid.CreateVersion7(), "Workspace", DateTimeOffset.UtcNow);
        var tenantId = workspace.Id;
        var group = DirectoryGroup.Create(tenantId, "Platform Group", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var membershipId = Guid.NewGuid();
        var groups = new DirectoryGroupRepositoryStub { Existing = group };
        var groupMemberships = new GroupMembershipRepositoryStub();
        var handler = new AddGroupMemberHandler(
            new TenantContextStub(tenantId),
            new AuthorizationStub(true),
            groups,
            groupMemberships,
            new TenantMembershipLookupStub(membershipId),
            new SettingsRepositoryStub(workspace),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(new AddGroupMemberCommand(group.Id, membershipId), CancellationToken.None);

        Assert.Equal(membershipId, result.MembershipId);
        Assert.NotNull(groupMemberships.Added);
        Assert.Equal(2, workspace.AuthorizationEpoch);
    }

    [Fact]
    public async Task AssignGroupProjectRole_CreatesNewAssignment()
    {
        var workspace = Workspace.Create(Guid.CreateVersion7(), "Workspace", DateTimeOffset.UtcNow);
        var tenantId = workspace.Id;
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var group = DirectoryGroup.Create(tenantId, "Platform Group", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var projectGroupRoles = new ProjectGroupRoleRepositoryStub();
        var handler = new AssignGroupProjectRoleHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            new DirectoryGroupRepositoryStub { Existing = group },
            projectGroupRoles,
            new SettingsRepositoryStub(workspace),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new AssignGroupProjectRoleCommand(project.Id, group.Id, ProjectRole.Member), CancellationToken.None);

        Assert.Equal(ProjectRole.Member, result.Role);
        Assert.NotNull(projectGroupRoles.Added);
        Assert.Equal(2, workspace.AuthorizationEpoch);
    }

    [Fact]
    public async Task AssignGroupProjectRole_UpdatesExistingAssignment()
    {
        var workspace = Workspace.Create(Guid.CreateVersion7(), "Workspace", DateTimeOffset.UtcNow);
        var tenantId = workspace.Id;
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var group = DirectoryGroup.Create(tenantId, "Platform Group", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var existing = ProjectGroupRoleAssignment.Create(
            tenantId, project.Id, group.Id, ProjectRole.Viewer, DateTimeOffset.UtcNow);
        var projectGroupRoles = new ProjectGroupRoleRepositoryStub { Existing = existing };
        var handler = new AssignGroupProjectRoleHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            new DirectoryGroupRepositoryStub { Existing = group },
            projectGroupRoles,
            new SettingsRepositoryStub(workspace),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new AssignGroupProjectRoleCommand(project.Id, group.Id, ProjectRole.Administrator), CancellationToken.None);

        Assert.Equal(ProjectRole.Administrator, result.Role);
        Assert.Null(projectGroupRoles.Added);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class CurrentPrincipalStub(Guid membershipId) : ICurrentPrincipal
    {
        public Guid? UserId => Guid.NewGuid();
        public Guid? SessionId => null;
        public Guid MembershipId => membershipId;
        public PrincipalType PrincipalType => PrincipalType.User;
        public TenantRole TenantRole => TenantRole.Owner;
        public MembershipTier MembershipTier => MembershipTier.Standard;
        public bool IsDevelopmentBypass => false;
    }

    private sealed class AuthorizationStub(bool allowed) : ITenantAuthorization
    {
        public bool CanCreateProject() => allowed;
        public bool CanCreateMembership(TenantRole role) => allowed;
        public bool CanManageTeams() => allowed;
    }

    private sealed class ProjectRepositoryStub(Project project, ProjectPermission[] allowedPermissions) : IProjectRepository
    {
        public Task AddAsync(Project value, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Project?> GetAsync(
            Guid tenantId,
            Guid projectId,
            ProjectPermission permission,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                project.Id == projectId && project.TenantId == tenantId && allowedPermissions.Contains(permission)
                    ? project
                    : null);

        public Task<PagedResult<Project>> ListAsync(
            Guid tenantId,
            ProjectPermission permission,
            int skip,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<Project>([project], 1));
    }

    private sealed class DirectoryGroupRepositoryStub : IDirectoryGroupRepository
    {
        public DirectoryGroup? Added { get; private set; }
        public DirectoryGroup? Existing { get; set; }

        public Task AddAsync(DirectoryGroup group, CancellationToken cancellationToken)
        {
            Added = group;
            return Task.CompletedTask;
        }

        public Task<DirectoryGroup?> GetAsync(Guid tenantId, Guid groupId, CancellationToken cancellationToken) =>
            Task.FromResult(Existing?.Id == groupId ? Existing : null);

        public Task<IReadOnlyList<DirectoryGroup>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DirectoryGroup>>(Existing is null ? [] : [Existing]);
    }

    private sealed class GroupMembershipRepositoryStub : IGroupMembershipRepository
    {
        public GroupMembership? Added { get; private set; }
        public GroupMembership? Removed { get; private set; }
        public GroupMembership? Existing { get; set; }

        public Task AddAsync(GroupMembership membership, CancellationToken cancellationToken)
        {
            Added = membership;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(GroupMembership membership, CancellationToken cancellationToken)
        {
            Removed = membership;
            return Task.CompletedTask;
        }

        public Task<GroupMembership?> GetAsync(
            Guid tenantId, Guid groupId, Guid membershipId, CancellationToken cancellationToken) =>
            Task.FromResult(
                Existing?.GroupId == groupId && Existing.MembershipId == membershipId ? Existing : null);

        public Task<IReadOnlyList<GroupMembership>> ListByGroupAsync(
            Guid tenantId, Guid groupId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GroupMembership>>(Existing is null ? [] : [Existing]);
    }

    private sealed class ProjectGroupRoleRepositoryStub : IProjectGroupRoleRepository
    {
        public ProjectGroupRoleAssignment? Added { get; private set; }
        public ProjectGroupRoleAssignment? Existing { get; set; }

        public Task<ProjectGroupRoleAssignment?> GetAsync(
            Guid tenantId, Guid projectId, Guid groupId, CancellationToken cancellationToken) =>
            Task.FromResult(
                Existing?.ProjectId == projectId && Existing.GroupId == groupId ? Existing : null);

        public Task AddAsync(ProjectGroupRoleAssignment assignment, CancellationToken cancellationToken)
        {
            Added = assignment;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ProjectGroupRoleAssignment>> ListByProjectAsync(
            Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProjectGroupRoleAssignment>>(Existing is null ? [] : [Existing]);
    }

    private sealed class TenantMembershipLookupStub(Guid membershipId) : ITenantMembershipRepository
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
            Guid tenantId, Guid requestedMembershipId, CancellationToken cancellationToken) =>
            Task.FromResult(
                requestedMembershipId == membershipId
                    ? TenantMembership.CreateForUser(tenantId, Guid.NewGuid(), TenantRole.Member, DateTimeOffset.UtcNow)
                    : null);

        public Task<TenantMembership?> GetOwnerAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<IReadOnlyList<TenantMembership>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<IReadOnlyList<TenantMembership>> ListByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> membershipIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<IReadOnlyList<Guid>> ListActiveUserIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    private sealed class SettingsRepositoryStub(Workspace workspace) : ISettingsRepository
    {
        public Task<UserAccount?> GetUserAccountAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserAccount?>(null);

        public Task<IReadOnlyList<UserAccount>> GetUserAccountsAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UserAccount>>([]);

        public Task<UserPreference?> GetUserPreferenceAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserPreference?>(null);

        public Task<NotificationPreference?> GetNotificationPreferenceAsync(
            Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<NotificationPreference?>(null);

        public Task<IReadOnlyList<NotificationPreference>> GetNotificationPreferencesAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NotificationPreference>>([]);

        public Task<Workspace?> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(workspace.Id == tenantId ? workspace : null);

        public Task<WorkspaceSetting?> GetWorkspaceSettingAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<WorkspaceSetting?>(null);

        public Task<WorkspaceTypographySetting?> GetWorkspaceTypographySettingAsync(
            Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<WorkspaceTypographySetting?>(null);

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

        public Task AddWorkspaceTypographySettingAsync(
            WorkspaceTypographySetting setting, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddProjectSettingAsync(ProjectSetting setting, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
