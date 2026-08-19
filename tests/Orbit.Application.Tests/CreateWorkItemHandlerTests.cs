using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.WorkItems;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.Configuration;
using Orbit.Domain.Identity;
using Orbit.Domain.Messaging;
using Orbit.Domain.Projects;
using Orbit.Domain.Settings;
using Orbit.Domain.WorkItems;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Tests;

public sealed class CreateWorkItemHandlerTests
{
    [Fact]
    public async Task Handle_AllocatesProjectSequenceAndPersistsItem()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var projects = new ProjectRepositoryStub(project);
        var workItems = new WorkItemRepositoryStub();
        var unitOfWork = new UnitOfWorkStub();
        var handler = new CreateWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            projects,
            new WorkItemTypeRepositoryStub(tenantId),
            workItems,
            new TenantMembershipRepositoryStub(),
            new SettingsRepositoryStub(),
            new OutboxRepositoryStub(),
            unitOfWork,
            TimeProvider.System);

        var result = await handler.Handle(
            new CreateWorkItemCommand(project.Id, "Build the board", null, WorkItemType.Story, Priority.High),
            CancellationToken.None);

        Assert.Equal("ORB-1", result.Key);
        Assert.NotNull(workItems.Added);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Handle_RejectsDisabledWorkspaceItemType()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var handler = new CreateWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new ProjectRepositoryStub(project),
            new WorkItemTypeRepositoryStub(tenantId, WorkItemType.Story),
            new WorkItemRepositoryStub(),
            new TenantMembershipRepositoryStub(),
            new SettingsRepositoryStub(),
            new OutboxRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new CreateWorkItemCommand(project.Id, "Build the board", null, WorkItemType.Story, Priority.High),
            CancellationToken.None);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(action);
    }

    [Fact]
    public async Task Handle_AssignsToActiveTenantMemberAndNotifiesThem()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var assigneeAccount = UserAccount.Create("assignee@example.com", "Assignee", DateTimeOffset.UtcNow);
        var assigneeUserId = assigneeAccount.Id;
        var outbox = new OutboxRepositoryStub();
        var handler = new CreateWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new ProjectRepositoryStub(project),
            new WorkItemTypeRepositoryStub(tenantId),
            new WorkItemRepositoryStub(),
            new TenantMembershipRepositoryStub(tenantId, assigneeUserId),
            new SettingsRepositoryStub([assigneeAccount]),
            outbox,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new CreateWorkItemCommand(
                project.Id, "Build the board", null, WorkItemType.Story, Priority.High,
                AssigneeUserId: assigneeUserId),
            CancellationToken.None);

        Assert.Equal(assigneeUserId, result.AssigneeUserId);
        var email = Assert.Single(outbox.Messages);
        Assert.Equal(assigneeAccount.NormalizedEmail, email.ToEmail);
    }

    [Fact]
    public async Task Handle_RejectsAssigneeWhoIsNotAnActiveTenantMember()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var handler = new CreateWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new ProjectRepositoryStub(project),
            new WorkItemTypeRepositoryStub(tenantId),
            new WorkItemRepositoryStub(),
            new TenantMembershipRepositoryStub(),
            new SettingsRepositoryStub(),
            new OutboxRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new CreateWorkItemCommand(
                project.Id, "Build the board", null, WorkItemType.Story, Priority.High,
                AssigneeUserId: Guid.NewGuid()),
            CancellationToken.None);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(action);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class CurrentPrincipalStub : ICurrentPrincipal
    {
        public Guid? UserId => null;
        public Guid? SessionId => null;
        public Guid MembershipId => Guid.NewGuid();
        public PrincipalType PrincipalType => PrincipalType.User;
        public TenantRole TenantRole => TenantRole.Owner;
        public MembershipTier MembershipTier => MembershipTier.Standard;
        public bool IsDevelopmentBypass => true;
    }

    private sealed class ProjectRepositoryStub(Project project) : IProjectRepository
    {
        public Task AddAsync(Project value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Project?> GetAsync(
            Guid tenantId,
            Guid projectId,
            ProjectPermission permission,
            CancellationToken cancellationToken) =>
            Task.FromResult<Project?>(project.Id == projectId && project.TenantId == tenantId ? project : null);
        public Task<PagedResult<Project>> ListAsync(
            Guid tenantId,
            ProjectPermission permission,
            int skip,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<Project>([project], 1));
    }

    private sealed class WorkItemRepositoryStub : IWorkItemRepository
    {
        public WorkItem? Added { get; private set; }
        public Task AddAsync(WorkItem workItem, CancellationToken cancellationToken)
        {
            Added = workItem;
            return Task.CompletedTask;
        }
        public Task<WorkItem?> GetAsync(
            Guid tenantId,
            Guid workItemId,
            ProjectPermission permission,
            CancellationToken cancellationToken) =>
            Task.FromResult<WorkItem?>(null);
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

    private sealed class WorkItemTypeRepositoryStub : IWorkItemTypeRepository
    {
        private readonly IReadOnlyList<WorkItemTypeDefinition> definitions;

        public WorkItemTypeRepositoryStub(Guid tenantId, WorkItemType? disabled = null)
        {
            definitions = WorkItemTypeDefinition.CreateSoftwareDefaults(tenantId, DateTimeOffset.UtcNow);
            if (disabled.HasValue)
            {
                var definition = definitions.Single(itemType => itemType.Id == disabled.Value);
                definition.Update(
                    definition.Label,
                    definition.Description,
                    definition.Order,
                    definition.ColorToken,
                    false,
                    DateTimeOffset.UtcNow);
            }
        }

        public Task<WorkItemTypeDefinition?> GetAsync(
            Guid requestedTenantId,
            WorkItemType id,
            CancellationToken cancellationToken) =>
            Task.FromResult(definitions.SingleOrDefault(
                definition => definition.TenantId == requestedTenantId && definition.Id == id));

        public Task<IReadOnlyList<WorkItemTypeDefinition>> ListAsync(
            Guid requestedTenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItemTypeDefinition>>(
                definitions.Where(definition => definition.TenantId == requestedTenantId).ToArray());
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
