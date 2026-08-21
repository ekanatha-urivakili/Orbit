using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.Settings;
using Orbit.Domain.Access;
using Orbit.Domain.Identity;
using Orbit.Domain.Messaging;
using Orbit.Domain.Projects;
using Orbit.Domain.Settings;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Tests;

public sealed class BoardViewPreferenceHandlerTests
{
    [Fact]
    public async Task GetBoardViewPreference_ReturnsZeroVersionSentinel_WhenNoneExists()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var handler = new GetBoardViewPreferenceHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(userId),
            new ProjectRepositoryStub(project),
            new SettingsRepositoryStub());

        var result = await handler.Handle(new GetBoardViewPreferenceQuery(project.Id), CancellationToken.None);

        Assert.Equal(0, result.Version);
        Assert.Equal(HideDoneItemsAfter.Never, result.HideDoneItemsAfter);
        Assert.Empty(result.HiddenFields);
    }

    [Fact]
    public async Task UpdateBoardViewPreference_CreatesOnFirstSave()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var settings = new SettingsRepositoryStub();
        var handler = new UpdateBoardViewPreferenceHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(userId),
            new ProjectRepositoryStub(project),
            settings,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new UpdateBoardViewPreferenceCommand(project.Id, HideDoneItemsAfter.OneWeek, BoardColumnSizeMode.Fixed, ["priority"], 0),
            CancellationToken.None);

        Assert.Equal(HideDoneItemsAfter.OneWeek, result.HideDoneItemsAfter);
        Assert.Equal(BoardColumnSizeMode.Fixed, result.ColumnSizeMode);
        Assert.Equal(["priority"], result.HiddenFields);
        Assert.NotNull(settings.Added);
    }

    [Fact]
    public async Task UpdateBoardViewPreference_RejectsStaleVersion()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var existing = BoardViewPreference.Create(tenantId, userId, project.Id, DateTimeOffset.UtcNow);
        var handler = new UpdateBoardViewPreferenceHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(userId),
            new ProjectRepositoryStub(project),
            new SettingsRepositoryStub(existing),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new UpdateBoardViewPreferenceCommand(project.Id, HideDoneItemsAfter.Never, BoardColumnSizeMode.Flexible, [], 5),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConcurrencyException>(action);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class CurrentPrincipalStub(Guid? userId) : ICurrentPrincipal
    {
        public Guid? UserId => userId;
        public Guid? SessionId => null;
        public Guid MembershipId => Guid.NewGuid();
        public PrincipalType PrincipalType => PrincipalType.User;
        public TenantRole TenantRole => TenantRole.Member;
        public MembershipTier MembershipTier => MembershipTier.Standard;
        public bool IsDevelopmentBypass => true;
    }

    private sealed class ProjectRepositoryStub(Project project) : IProjectRepository
    {
        public Task AddAsync(Project value, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Project?> GetAsync(
            Guid tenantId, Guid projectId, ProjectPermission permission, CancellationToken cancellationToken) =>
            Task.FromResult<Project?>(project.Id == projectId && project.TenantId == tenantId ? project : null);

        public Task<PagedResult<Project>> ListAsync(
            Guid tenantId, ProjectPermission permission, int skip, int take, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<Project>([project], 1));
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private sealed class SettingsRepositoryStub(BoardViewPreference? existing = null) : ISettingsRepository
    {
        public BoardViewPreference? Added { get; private set; }

        public Task<UserAccount?> GetUserAccountAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserAccount?>(null);

        public Task<IReadOnlyList<UserAccount>> GetUserAccountsAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UserAccount>>([]);

        public Task<UserPreference?> GetUserPreferenceAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserPreference?>(null);

        public Task<IReadOnlyList<UserPreference>> GetUserPreferencesAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UserPreference>>([]);

        public Task<NotificationPreference?> GetNotificationPreferenceAsync(
            Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<NotificationPreference?>(null);

        public Task<IReadOnlyList<NotificationPreference>> GetNotificationPreferencesAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NotificationPreference>>([]);

        public Task<Workspace?> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<Workspace?>(null);

        public Task<WorkspaceSetting?> GetWorkspaceSettingAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<WorkspaceSetting?>(null);

        public Task<WorkspaceTypographySetting?> GetWorkspaceTypographySettingAsync(
            Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<WorkspaceTypographySetting?>(null);

        public Task<ProjectSetting?> GetProjectSettingAsync(
            Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<ProjectSetting?>(null);

        public Task<BoardViewPreference?> GetBoardViewPreferenceAsync(
            Guid tenantId, Guid userId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(existing);

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

        public Task AddBoardViewPreferenceAsync(BoardViewPreference preference, CancellationToken cancellationToken)
        {
            Added = preference;
            return Task.CompletedTask;
        }
    }
}
