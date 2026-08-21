using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.WorkItems;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Choices;
using Orbit.Domain.Configuration;
using Orbit.Domain.Identity;
using Orbit.Domain.Messaging;
using Orbit.Domain.Settings;
using Orbit.Domain.WorkItems;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Tests;

public sealed class ChangeWorkItemStatusHandlerTests
{
    [Fact]
    public async Task Handle_TransitioningStatus_EnqueuesOutboxEmailForAssignee()
    {
        var tenantId = Guid.NewGuid();
        var authorUserId = Guid.NewGuid();
        var assigneeAccount = UserAccount.Create("assignee@example.com", "Assignee", DateTimeOffset.UtcNow);
        var (workItem, statuses, inProgress) = CreateWorkItem(tenantId, assigneeAccount.Id);
        var settings = new SettingsRepositoryStub([assigneeAccount], preferences: []);
        var outbox = new OutboxRepositoryStub();
        var handler = new ChangeWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(authorUserId),
            new WorkItemRepositoryStub(workItem),
            new WorkItemStatusRepositoryStub(statuses),
            new SprintMembershipRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            settings,
            outbox,
            new WorkItemHistoryRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(
            new ChangeWorkItemStatusCommand(workItem.Id, inProgress.Id, workItem.Version),
            CancellationToken.None);

        var email = Assert.Single(outbox.Messages);
        Assert.Equal(assigneeAccount.NormalizedEmail, email.ToEmail);
        Assert.Contains(workItem.Key, email.Subject);
    }

    [Fact]
    public async Task Handle_AssigneeHasEmailDisabled_DoesNotEnqueueOutboxEmail()
    {
        var tenantId = Guid.NewGuid();
        var authorUserId = Guid.NewGuid();
        var assigneeAccount = UserAccount.Create("assignee@example.com", "Assignee", DateTimeOffset.UtcNow);
        var preference = NotificationPreference.Create(assigneeAccount.Id, DateTimeOffset.UtcNow);
        preference.Update(true, false, DigestCadence.Daily, null, null, false, DateTimeOffset.UtcNow);
        var (workItem, statuses, inProgress) = CreateWorkItem(tenantId, assigneeAccount.Id);
        var settings = new SettingsRepositoryStub([assigneeAccount], [preference]);
        var outbox = new OutboxRepositoryStub();
        var handler = new ChangeWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(authorUserId),
            new WorkItemRepositoryStub(workItem),
            new WorkItemStatusRepositoryStub(statuses),
            new SprintMembershipRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            settings,
            outbox,
            new WorkItemHistoryRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(
            new ChangeWorkItemStatusCommand(workItem.Id, inProgress.Id, workItem.Version),
            CancellationToken.None);

        Assert.Empty(outbox.Messages);
    }

    [Fact]
    public async Task Handle_CallerIsAssigneeWithoutSelfNotify_DoesNotEnqueueOutboxEmail()
    {
        var tenantId = Guid.NewGuid();
        var assigneeAccount = UserAccount.Create("assignee@example.com", "Assignee", DateTimeOffset.UtcNow);
        var (workItem, statuses, inProgress) = CreateWorkItem(tenantId, assigneeAccount.Id);
        var settings = new SettingsRepositoryStub([assigneeAccount], preferences: []);
        var outbox = new OutboxRepositoryStub();
        var handler = new ChangeWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(assigneeAccount.Id),
            new WorkItemRepositoryStub(workItem),
            new WorkItemStatusRepositoryStub(statuses),
            new SprintMembershipRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            settings,
            outbox,
            new WorkItemHistoryRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(
            new ChangeWorkItemStatusCommand(workItem.Id, inProgress.Id, workItem.Version),
            CancellationToken.None);

        Assert.Empty(outbox.Messages);
    }

    [Fact]
    public async Task Handle_TransitioningStatus_RecordsHistoryEntry()
    {
        var tenantId = Guid.NewGuid();
        var authorUserId = Guid.NewGuid();
        var assigneeAccount = UserAccount.Create("assignee@example.com", "Assignee", DateTimeOffset.UtcNow);
        var (workItem, statuses, inProgress) = CreateWorkItem(tenantId, assigneeAccount.Id);
        var settings = new SettingsRepositoryStub([assigneeAccount], preferences: []);
        var history = new WorkItemHistoryRepositoryStub();
        var handler = new ChangeWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(authorUserId),
            new WorkItemRepositoryStub(workItem),
            new WorkItemStatusRepositoryStub(statuses),
            new SprintMembershipRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            settings,
            new OutboxRepositoryStub(),
            history,
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(
            new ChangeWorkItemStatusCommand(workItem.Id, inProgress.Id, workItem.Version),
            CancellationToken.None);

        var entry = Assert.Single(history.Added);
        Assert.Equal("Status", entry.FieldName);
        Assert.Equal("backlog", entry.OldValue);
        Assert.Equal("in-progress", entry.NewValue);
    }

    [Fact]
    public async Task Handle_StatusUnchanged_DoesNotRecordHistory()
    {
        var tenantId = Guid.NewGuid();
        var authorUserId = Guid.NewGuid();
        var assigneeAccount = UserAccount.Create("assignee@example.com", "Assignee", DateTimeOffset.UtcNow);
        var (workItem, statuses, _) = CreateWorkItem(tenantId, assigneeAccount.Id);
        var settings = new SettingsRepositoryStub([assigneeAccount], preferences: []);
        var history = new WorkItemHistoryRepositoryStub();
        var handler = new ChangeWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(authorUserId),
            new WorkItemRepositoryStub(workItem),
            new WorkItemStatusRepositoryStub(statuses),
            new SprintMembershipRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            settings,
            new OutboxRepositoryStub(),
            history,
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(
            new ChangeWorkItemStatusCommand(workItem.Id, workItem.StatusId, workItem.Version),
            CancellationToken.None);

        Assert.Empty(history.Added);
    }

    [Fact]
    public async Task Handle_StatusUnchanged_DoesNotEnqueueOutboxEmail()
    {
        var tenantId = Guid.NewGuid();
        var authorUserId = Guid.NewGuid();
        var assigneeAccount = UserAccount.Create("assignee@example.com", "Assignee", DateTimeOffset.UtcNow);
        var (workItem, statuses, _) = CreateWorkItem(tenantId, assigneeAccount.Id);
        var settings = new SettingsRepositoryStub([assigneeAccount], preferences: []);
        var outbox = new OutboxRepositoryStub();
        var handler = new ChangeWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(authorUserId),
            new WorkItemRepositoryStub(workItem),
            new WorkItemStatusRepositoryStub(statuses),
            new SprintMembershipRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            settings,
            outbox,
            new WorkItemHistoryRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(
            new ChangeWorkItemStatusCommand(workItem.Id, workItem.StatusId, workItem.Version),
            CancellationToken.None);

        Assert.Empty(outbox.Messages);
    }

    [Fact]
    public async Task Handle_NoOwnersAssigned_DoesNotEnqueueOutboxEmail()
    {
        var tenantId = Guid.NewGuid();
        var authorUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, projectId, DateTimeOffset.UtcNow);
        var backlog = statuses.Single(status => status.Key == "backlog");
        var inProgress = statuses.Single(status => status.Key == "in-progress");
        var workItem = WorkItem.Create(
            tenantId, projectId, 1, "ORB", "Build the board", null, WorkItemType.Story, Priority.High,
            backlog.Id, DateTimeOffset.UtcNow);
        var settings = new SettingsRepositoryStub([], preferences: []);
        var outbox = new OutboxRepositoryStub();
        var handler = new ChangeWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(authorUserId),
            new WorkItemRepositoryStub(workItem),
            new WorkItemStatusRepositoryStub(statuses),
            new SprintMembershipRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            settings,
            outbox,
            new WorkItemHistoryRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(
            new ChangeWorkItemStatusCommand(workItem.Id, inProgress.Id, workItem.Version),
            CancellationToken.None);

        Assert.Empty(outbox.Messages);
    }

    private static (WorkItem WorkItem, IReadOnlyList<WorkItemStatusDefinition> Statuses, WorkItemStatusDefinition InProgress) CreateWorkItem(
        Guid tenantId, Guid assigneeUserId)
    {
        var projectId = Guid.NewGuid();
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, projectId, DateTimeOffset.UtcNow);
        var backlog = statuses.Single(status => status.Key == "backlog");
        var inProgress = statuses.Single(status => status.Key == "in-progress");
        var workItem = WorkItem.Create(
            tenantId, projectId, 1, "ORB", "Build the board", null, WorkItemType.Story, Priority.High,
            backlog.Id, DateTimeOffset.UtcNow);
        workItem.SetDetails(
            parentId: null,
            epicName: null,
            acceptanceCriteria: null,
            stepsToConduct: null,
            assigneeUserId: assigneeUserId,
            developerUserId: null,
            productOwnerUserId: null,
            sprintName: null,
            identifiedOn: null,
            startDate: null,
            dueDate: null,
            teamId: null,
            storyPoints: null,
            labels: null,
            countries: null,
            attachmentNames: null);
        return (workItem, statuses, inProgress);
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

    private sealed class WorkItemRepositoryStub(WorkItem workItem) : IWorkItemRepository
    {
        public Task AddAsync(WorkItem value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<WorkItem?> GetAsync(
            Guid tenantId,
            Guid workItemId,
            ProjectPermission permission,
            CancellationToken cancellationToken) =>
            Task.FromResult<WorkItem?>(workItem.Id == workItemId && workItem.TenantId == tenantId ? workItem : null);
        public Task<PagedResult<WorkItem>> ListByProjectAsync(
            Guid tenantId,
            Guid projectId,
            ProjectPermission permission,
            int skip,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<WorkItem>([], 0));
        public Task<IReadOnlyList<WorkItem>> ListByIdsAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> workItemIds,
            ProjectPermission permission,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItem>>([]);
        public Task<bool> HasChildrenAsync(Guid tenantId, Guid parentWorkItemId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task RemoveAsync(WorkItem workItem, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class WorkItemStatusRepositoryStub(IReadOnlyList<WorkItemStatusDefinition> statuses) : IWorkItemStatusRepository
    {
        public Task AddAsync(WorkItemStatusDefinition definition, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddRangeAsync(IReadOnlyCollection<WorkItemStatusDefinition> definitions, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<WorkItemStatusDefinition?> GetAsync(
            Guid tenantId, Guid projectId, Guid statusId, CancellationToken cancellationToken) =>
            Task.FromResult(statuses.SingleOrDefault(status =>
                status.TenantId == tenantId && status.ProjectId == projectId && status.Id == statusId));

        public Task<IReadOnlyList<WorkItemStatusDefinition>> ListByProjectAsync(
            Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItemStatusDefinition>>(
                [.. statuses.Where(status => status.TenantId == tenantId && status.ProjectId == projectId)]);

        public Task<WorkItemStatusDefinition?> GetDefaultAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(statuses
                .Where(status => status.TenantId == tenantId && status.ProjectId == projectId)
                .OrderBy(status => status.Order)
                .FirstOrDefault());

        public Task<bool> IsInUseAsync(Guid tenantId, Guid projectId, Guid statusId, string statusKey, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task RemoveAsync(WorkItemStatusDefinition definition, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SprintMembershipRepositoryStub : ISprintMembershipRepository
    {
        public Task AddAsync(SprintMembership membership, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<SprintMembership?> GetCurrentByWorkItemAsync(
            Guid tenantId, Guid workItemId, CancellationToken cancellationToken) =>
            Task.FromResult<SprintMembership?>(null);
        public Task<IReadOnlyList<SprintMembership>> ListCurrentBySprintAsync(
            Guid tenantId, Guid sprintId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SprintMembership>>([]);
        public Task<IReadOnlyList<SprintMembership>> ListCurrentBySprintsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> sprintIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SprintMembership>>([]);
    }

    private sealed class SprintScopeFactRepositoryStub : ISprintScopeFactRepository
    {
        public Task AddAsync(SprintScopeFact fact, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<SprintScopeFact>> ListBySprintAsync(
            Guid tenantId, Guid sprintId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SprintScopeFact>>([]);
    }

    private sealed class SettingsRepositoryStub(
        IReadOnlyList<UserAccount> accounts,
        IReadOnlyList<NotificationPreference> preferences) : ISettingsRepository
    {
        public Task<UserAccount?> GetUserAccountAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(accounts.SingleOrDefault(a => a.Id == userId));

        public Task<IReadOnlyList<UserAccount>> GetUserAccountsAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UserAccount>>(
                accounts.Where(a => userIds.Contains(a.Id)).ToArray());

        public Task<UserPreference?> GetUserPreferenceAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserPreference?>(null);

        public Task<IReadOnlyList<UserPreference>> GetUserPreferencesAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UserPreference>>([]);

        public Task<NotificationPreference?> GetNotificationPreferenceAsync(
            Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(preferences.SingleOrDefault(p => p.UserId == userId));

        public Task<IReadOnlyList<NotificationPreference>> GetNotificationPreferencesAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NotificationPreference>>(
                preferences.Where(p => userIds.Contains(p.UserId)).ToArray());

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
            Task.FromResult<BoardViewPreference?>(null);

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

        public Task AddBoardViewPreferenceAsync(BoardViewPreference preference, CancellationToken cancellationToken) =>
            Task.CompletedTask;
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

    private sealed class WorkItemHistoryRepositoryStub : IWorkItemHistoryRepository
    {
        public List<WorkItemHistoryEntry> Added { get; } = [];

        public Task AddAsync(WorkItemHistoryEntry entry, CancellationToken cancellationToken)
        {
            Added.Add(entry);
            return Task.CompletedTask;
        }

        public Task<PagedResult<WorkItemHistoryEntry>> ListByWorkItemAsync(
            Guid tenantId, Guid workItemId, int skip, int take, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<WorkItemHistoryEntry>([], 0));

        public Task<IReadOnlyList<WorkItemHistoryEntry>> ListByWorkItemsAndFieldAsync(
            Guid tenantId, IReadOnlyCollection<Guid> workItemIds, string fieldName, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItemHistoryEntry>>(
                Added.Where(e => workItemIds.Contains(e.WorkItemId) && e.FieldName == fieldName).ToArray());
    }
}
