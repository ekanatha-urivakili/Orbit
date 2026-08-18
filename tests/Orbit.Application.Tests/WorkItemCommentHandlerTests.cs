using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.WorkItems;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.Common;
using Orbit.Domain.Identity;
using Orbit.Domain.Messaging;
using Orbit.Domain.Settings;
using Orbit.Domain.WorkItems;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Tests;

public sealed class WorkItemCommentHandlerTests
{
    [Fact]
    public async Task Handle_MentioningAnotherUser_EnqueuesOutboxEmail()
    {
        var tenantId = Guid.NewGuid();
        var authorUserId = Guid.NewGuid();
        var mentionedUserId = Guid.NewGuid();
        var mentionedAccount = UserAccount.Create("mentioned@example.com", "Mentioned User", DateTimeOffset.UtcNow);
        var workItem = CreateWorkItem(tenantId);
        var settings = new SettingsRepositoryStub([mentionedAccount], preferences: []);
        var outbox = new OutboxRepositoryStub();
        var handler = new AddWorkItemCommentHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(authorUserId),
            new WorkItemRepositoryStub(workItem),
            new WorkItemCommentRepositoryStub(),
            settings,
            outbox,
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(
            new AddWorkItemCommentCommand(workItem.Id, $"Hey @{{{mentionedAccount.Id}}}, take a look"),
            CancellationToken.None);

        var email = Assert.Single(outbox.Messages);
        Assert.Equal(mentionedAccount.NormalizedEmail, email.ToEmail);
        Assert.Contains(workItem.Key, email.Subject);
    }

    [Fact]
    public async Task Handle_MentionedUserHasEmailDisabled_DoesNotEnqueueOutboxEmail()
    {
        var tenantId = Guid.NewGuid();
        var authorUserId = Guid.NewGuid();
        var mentionedAccount = UserAccount.Create("mentioned@example.com", "Mentioned User", DateTimeOffset.UtcNow);
        var preference = NotificationPreference.Create(mentionedAccount.Id, DateTimeOffset.UtcNow);
        preference.Update(true, false, DigestCadence.Daily, null, null, false, DateTimeOffset.UtcNow);
        var workItem = CreateWorkItem(tenantId);
        var settings = new SettingsRepositoryStub([mentionedAccount], [preference]);
        var outbox = new OutboxRepositoryStub();
        var handler = new AddWorkItemCommentHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(authorUserId),
            new WorkItemRepositoryStub(workItem),
            new WorkItemCommentRepositoryStub(),
            settings,
            outbox,
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(
            new AddWorkItemCommentCommand(workItem.Id, $"Hey @{{{mentionedAccount.Id}}}, take a look"),
            CancellationToken.None);

        Assert.Empty(outbox.Messages);
    }

    [Fact]
    public async Task Handle_SelfMentionWithoutSelfNotify_DoesNotEnqueueOutboxEmail()
    {
        var tenantId = Guid.NewGuid();
        var authorAccount = UserAccount.Create("author@example.com", "Author", DateTimeOffset.UtcNow);
        var workItem = CreateWorkItem(tenantId);
        var settings = new SettingsRepositoryStub([authorAccount], preferences: []);
        var outbox = new OutboxRepositoryStub();
        var handler = new AddWorkItemCommentHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(authorAccount.Id),
            new WorkItemRepositoryStub(workItem),
            new WorkItemCommentRepositoryStub(),
            settings,
            outbox,
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(
            new AddWorkItemCommentCommand(workItem.Id, $"Note to self @{{{authorAccount.Id}}}"),
            CancellationToken.None);

        Assert.Empty(outbox.Messages);
    }

    private static WorkItem CreateWorkItem(Guid tenantId) =>
        WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Build the board", null, WorkItemType.Story, Priority.High,
            DateTimeOffset.UtcNow);

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
    }

    private sealed class WorkItemCommentRepositoryStub : IWorkItemCommentRepository
    {
        public Task AddAsync(WorkItemComment comment, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<WorkItemComment?> GetAsync(
            Guid tenantId, Guid workItemId, Guid commentId, CancellationToken cancellationToken) =>
            Task.FromResult<WorkItemComment?>(null);
        public Task<IReadOnlyList<WorkItemComment>> ListByWorkItemAsync(
            Guid tenantId, Guid workItemId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItemComment>>([]);
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

        public Task<NotificationPreference?> GetNotificationPreferenceAsync(
            Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(preferences.SingleOrDefault(p => p.UserId == userId));

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
}
