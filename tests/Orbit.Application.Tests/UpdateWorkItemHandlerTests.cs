using FluentValidation;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.WorkItems;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Choices;
using Orbit.Domain.Identity;
using Orbit.Domain.Messaging;
using Orbit.Domain.Settings;
using Orbit.Domain.WorkItems;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Tests;

public sealed class UpdateWorkItemHandlerTests
{
    private static WorkItem NewItem(
        Guid tenantId, Guid projectId, WorkItemType type = WorkItemType.Task, long sequenceNumber = 1) =>
        WorkItem.Create(
            tenantId, projectId, sequenceNumber, "ORB", $"Card {sequenceNumber}", null,
            type, Priority.Medium, DateTimeOffset.UtcNow);

    private static UpdateWorkItemCommand CommandFor(WorkItem item, string summary = "Updated summary") =>
        new(
            item.Id, summary, "Updated description", Priority.High,
            /* ParentId */ null, /* EpicName */ null, /* AcceptanceCriteria */ null, /* StepsToConduct */ null,
            /* AssigneeUserId */ null, /* DeveloperUserId */ null, /* ProductOwnerUserId */ null,
            /* SprintName */ null, /* IdentifiedOn */ null, /* StoryPoints */ null,
            /* Labels */ null, /* Countries */ null, /* AttachmentNames */ null,
            item.Version);

    [Fact]
    public async Task Handle_UpdatesFieldsAndBumpsVersion()
    {
        var tenantId = Guid.NewGuid();
        var item = NewItem(tenantId, Guid.NewGuid());
        var handler = new UpdateWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(null),
            new WorkItemRepositoryStub(item),
            new SprintMembershipRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            new TenantMembershipRepositoryStub(),
            new SettingsRepositoryStub(),
            new OutboxRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(CommandFor(item), CancellationToken.None);

        Assert.Equal("Updated summary", result.Summary);
        Assert.Equal("Updated description", result.Description);
        Assert.Equal(Priority.High, result.Priority);
        Assert.Equal(2, result.Version);
    }

    [Fact]
    public async Task Handle_RejectsStaleVersion()
    {
        var tenantId = Guid.NewGuid();
        var item = NewItem(tenantId, Guid.NewGuid());
        var handler = new UpdateWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(null),
            new WorkItemRepositoryStub(item),
            new SprintMembershipRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            new TenantMembershipRepositoryStub(),
            new SettingsRepositoryStub(),
            new OutboxRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(CommandFor(item) with { ExpectedVersion = item.Version + 1 }, CancellationToken.None);

        await Assert.ThrowsAsync<ConcurrencyException>(action);
    }

    [Fact]
    public async Task Handle_HidesExistence_WhenWorkItemNotVisible()
    {
        var tenantId = Guid.NewGuid();
        var handler = new UpdateWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(null),
            new WorkItemRepositoryStub(),
            new SprintMembershipRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            new TenantMembershipRepositoryStub(),
            new SettingsRepositoryStub(),
            new OutboxRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new UpdateWorkItemCommand(
                Guid.NewGuid(), "Updated summary", null, Priority.Medium,
                /* ParentId */ null, /* EpicName */ null, /* AcceptanceCriteria */ null, /* StepsToConduct */ null,
                /* AssigneeUserId */ null, /* DeveloperUserId */ null, /* ProductOwnerUserId */ null,
                /* SprintName */ null, /* IdentifiedOn */ null, /* StoryPoints */ null,
                /* Labels */ null, /* Countries */ null, /* AttachmentNames */ null,
                /* ExpectedVersion */ 1),
            CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    [Fact]
    public async Task Handle_RejectsAssigneeWhoIsNotAnActiveTenantMember()
    {
        var tenantId = Guid.NewGuid();
        var item = NewItem(tenantId, Guid.NewGuid());
        var handler = new UpdateWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(Guid.NewGuid()),
            new WorkItemRepositoryStub(item),
            new SprintMembershipRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            new TenantMembershipRepositoryStub(),
            new SettingsRepositoryStub(),
            new OutboxRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            CommandFor(item) with { AssigneeUserId = Guid.NewGuid() }, CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(action);
    }

    [Fact]
    public async Task Handle_ReassignsToAnotherActiveTenantMemberAndNotifiesThem()
    {
        var tenantId = Guid.NewGuid();
        var item = NewItem(tenantId, Guid.NewGuid());
        var assigneeAccount = UserAccount.Create("assignee@example.com", "Assignee", DateTimeOffset.UtcNow);
        var newAssigneeUserId = assigneeAccount.Id;
        var outbox = new OutboxRepositoryStub();
        var handler = new UpdateWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(Guid.NewGuid()),
            new WorkItemRepositoryStub(item),
            new SprintMembershipRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            new TenantMembershipRepositoryStub(tenantId, newAssigneeUserId),
            new SettingsRepositoryStub([assigneeAccount]),
            outbox,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            CommandFor(item) with { AssigneeUserId = newAssigneeUserId }, CancellationToken.None);

        Assert.Equal(newAssigneeUserId, result.AssigneeUserId);
        var email = Assert.Single(outbox.Messages);
        Assert.Equal(assigneeAccount.NormalizedEmail, email.ToEmail);
    }

    [Fact]
    public async Task Handle_ReassigningToSameAssignee_DoesNotSendAnotherNotification()
    {
        var tenantId = Guid.NewGuid();
        var assigneeUserId = Guid.NewGuid();
        var item = NewItem(tenantId, Guid.NewGuid());
        item.SetDetails(
            parentId: null, epicName: null, acceptanceCriteria: null, stepsToConduct: null,
            assigneeUserId: assigneeUserId, developerUserId: null, productOwnerUserId: null,
            sprintName: null, identifiedOn: null, storyPoints: null, labels: null, countries: null,
            attachmentNames: null);
        var assigneeAccount = UserAccount.Create("assignee@example.com", "Assignee", DateTimeOffset.UtcNow);
        var outbox = new OutboxRepositoryStub();
        var handler = new UpdateWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(Guid.NewGuid()),
            new WorkItemRepositoryStub(item),
            new SprintMembershipRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            new TenantMembershipRepositoryStub(tenantId, assigneeUserId),
            new SettingsRepositoryStub([assigneeAccount]),
            outbox,
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(CommandFor(item) with { AssigneeUserId = assigneeUserId }, CancellationToken.None);

        Assert.Empty(outbox.Messages);
    }

    [Fact]
    public async Task Handle_RejectsParentOutsideHierarchy()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var item = NewItem(tenantId, projectId, WorkItemType.Epic, 1);
        var invalidParent = NewItem(tenantId, projectId, WorkItemType.Task, 2);
        var handler = new UpdateWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(null),
            new WorkItemRepositoryStub(item, invalidParent),
            new SprintMembershipRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            new TenantMembershipRepositoryStub(),
            new SettingsRepositoryStub(),
            new OutboxRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            CommandFor(item) with { ParentId = invalidParent.Id, EpicName = "Epic name" }, CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(action);
    }

    [Fact]
    public async Task Handle_RecordsEstimateChangedFact_WhenSprintScopedItemsPointsChange()
    {
        var tenantId = Guid.NewGuid();
        var item = NewItem(tenantId, Guid.NewGuid());
        var sprintId = Guid.NewGuid();
        var membership = SprintMembership.Create(tenantId, sprintId, item.Id, DateTimeOffset.UtcNow);
        var facts = new SprintScopeFactRepositoryStub();
        var handler = new UpdateWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(null),
            new WorkItemRepositoryStub(item),
            new SprintMembershipRepositoryStub(membership),
            facts,
            new TenantMembershipRepositoryStub(),
            new SettingsRepositoryStub(),
            new OutboxRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(CommandFor(item) with { StoryPoints = 8 }, CancellationToken.None);

        var fact = Assert.Single(facts.Added);
        Assert.Equal(AgileFactType.EstimateChanged, fact.FactType);
        Assert.Equal(sprintId, fact.SprintId);
        Assert.Equal(8m, fact.EstimateDelta);
    }

    [Fact]
    public async Task Handle_DoesNotRecordEstimateChangedFact_WhenItemHasNoSprintMembership()
    {
        var tenantId = Guid.NewGuid();
        var item = NewItem(tenantId, Guid.NewGuid());
        var facts = new SprintScopeFactRepositoryStub();
        var handler = new UpdateWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(null),
            new WorkItemRepositoryStub(item),
            new SprintMembershipRepositoryStub(),
            facts,
            new TenantMembershipRepositoryStub(),
            new SettingsRepositoryStub(),
            new OutboxRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(CommandFor(item) with { StoryPoints = 8 }, CancellationToken.None);

        Assert.Empty(facts.Added);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class CurrentPrincipalStub(Guid? userId) : ICurrentPrincipal
    {
        public Guid? UserId => userId;
        public Guid? SessionId => null;
        public Guid MembershipId => Guid.NewGuid();
        public PrincipalType PrincipalType => PrincipalType.User;
        public TenantRole TenantRole => TenantRole.Owner;
        public MembershipTier MembershipTier => MembershipTier.Standard;
        public bool IsDevelopmentBypass => true;
    }

    private sealed class WorkItemRepositoryStub(params WorkItem[] items) : IWorkItemRepository
    {
        public Task AddAsync(WorkItem workItem, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<WorkItem?> GetAsync(
            Guid tenantId,
            Guid workItemId,
            ProjectPermission permission,
            CancellationToken cancellationToken) =>
            Task.FromResult(items.SingleOrDefault(item => item.Id == workItemId && item.TenantId == tenantId));

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
            Task.FromResult<IReadOnlyList<WorkItem>>(
                items.Where(item => item.TenantId == tenantId && workItemIds.Contains(item.Id)).ToArray());
    }

    private sealed class SprintMembershipRepositoryStub(SprintMembership? membership = null) : ISprintMembershipRepository
    {
        public Task AddAsync(SprintMembership value, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<SprintMembership?> GetCurrentByWorkItemAsync(
            Guid tenantId, Guid workItemId, CancellationToken cancellationToken) =>
            Task.FromResult(membership is not null && membership.WorkItemId == workItemId ? membership : null);

        public Task<IReadOnlyList<SprintMembership>> ListCurrentBySprintAsync(
            Guid tenantId, Guid sprintId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SprintMembership>>([]);

        public Task<IReadOnlyList<SprintMembership>> ListCurrentBySprintsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> sprintIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SprintMembership>>([]);
    }

    private sealed class SprintScopeFactRepositoryStub : ISprintScopeFactRepository
    {
        public List<SprintScopeFact> Added { get; } = [];

        public Task AddAsync(SprintScopeFact fact, CancellationToken cancellationToken)
        {
            Added.Add(fact);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SprintScopeFact>> ListBySprintAsync(
            Guid tenantId, Guid sprintId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SprintScopeFact>>([]);
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private sealed class TenantMembershipRepositoryStub(Guid tenantId = default, params Guid[] activeUserIds)
        : ITenantMembershipRepository
    {
        public Task AddAsync(TenantMembership membership, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<TenantMembership?> GetActiveAsync(
            Guid requestedTenantId, string issuer, string subject, CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<TenantMembership?> GetActiveByUserAsync(
            Guid requestedTenantId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(requestedTenantId == tenantId && activeUserIds.Contains(userId)
                ? TenantMembership.CreateForUser(tenantId, userId, TenantRole.Member, DateTimeOffset.UtcNow)
                : null);

        public Task<TenantMembership?> GetActiveAsync(
            Guid requestedTenantId, Guid membershipId, CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<TenantMembership?> GetOwnerAsync(Guid requestedTenantId, CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<IReadOnlyList<TenantMembership>> ListAsync(
            Guid requestedTenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<IReadOnlyList<TenantMembership>> ListByIdsAsync(
            Guid requestedTenantId, IReadOnlyCollection<Guid> membershipIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<IReadOnlyList<Guid>> ListActiveUserIdsAsync(
            Guid requestedTenantId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>(requestedTenantId == tenantId
                ? [.. userIds.Where(activeUserIds.Contains)]
                : []);
    }

    private sealed class SettingsRepositoryStub(params UserAccount[] accounts) : ISettingsRepository
    {
        public Task<UserAccount?> GetUserAccountAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(accounts.SingleOrDefault(a => a.Id == userId));

        public Task<IReadOnlyList<UserAccount>> GetUserAccountsAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UserAccount>>(accounts.Where(a => userIds.Contains(a.Id)).ToArray());

        public Task<UserPreference?> GetUserPreferenceAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserPreference?>(null);

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
}
