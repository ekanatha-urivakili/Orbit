using FluentValidation;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.WorkItems;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.Directory;
using Orbit.Domain.Identity;
using Orbit.Domain.Messaging;
using Orbit.Domain.Settings;
using Orbit.Domain.WorkItems;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Tests;

public sealed class ShareWorkItemHandlerTests
{
    [Fact]
    public async Task Handle_DirectMembership_EnqueuesOutboxEmail()
    {
        var tenantId = Guid.NewGuid();
        var recipientAccount = UserAccount.Create("teammate@example.com", "Teammate", DateTimeOffset.UtcNow);
        var recipientMembership = TenantMembership.CreateForUser(
            tenantId, recipientAccount.Id, TenantRole.Member, DateTimeOffset.UtcNow);
        var workItem = WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Share this card", null, WorkItemType.Story, Priority.High,
            DateTimeOffset.UtcNow);
        var outbox = new OutboxRepositoryStub();
        var handler = new ShareWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(Guid.NewGuid()),
            new WorkItemRepositoryStub(workItem),
            new TenantMembershipRepositoryStub([recipientMembership]),
            new TeamMembershipRepositoryStub([]),
            new SettingsRepositoryStub([recipientAccount]),
            outbox,
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(
            new ShareWorkItemCommand(workItem.Id, [recipientMembership.Id], [], "Check this out"),
            CancellationToken.None);

        var email = Assert.Single(outbox.Messages);
        Assert.Equal(recipientAccount.NormalizedEmail, email.ToEmail);
        Assert.Contains(workItem.Key, email.Subject);
    }

    [Fact]
    public async Task Handle_TeamMembership_FansOutToTeamMembers()
    {
        var tenantId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var recipientAccount = UserAccount.Create("teammate@example.com", "Teammate", DateTimeOffset.UtcNow);
        var recipientMembership = TenantMembership.CreateForUser(
            tenantId, recipientAccount.Id, TenantRole.Member, DateTimeOffset.UtcNow);
        var teamMembership = TeamMembership.Create(tenantId, teamId, recipientMembership.Id, DateTimeOffset.UtcNow);
        var workItem = WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Share this card", null, WorkItemType.Story, Priority.High,
            DateTimeOffset.UtcNow);
        var outbox = new OutboxRepositoryStub();
        var handler = new ShareWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(Guid.NewGuid()),
            new WorkItemRepositoryStub(workItem),
            new TenantMembershipRepositoryStub([recipientMembership]),
            new TeamMembershipRepositoryStub([teamMembership]),
            new SettingsRepositoryStub([recipientAccount]),
            outbox,
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(new ShareWorkItemCommand(workItem.Id, [], [teamId], null), CancellationToken.None);

        Assert.Single(outbox.Messages);
    }

    [Fact]
    public async Task Handle_NoRecipientsOrTeams_FailsValidation()
    {
        var validator = new ShareWorkItemValidator();

        var result = validator.Validate(new ShareWorkItemCommand(Guid.NewGuid(), [], [], null));

        Assert.False(result.IsValid);
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
            Guid tenantId, Guid workItemId, ProjectPermission permission, CancellationToken cancellationToken) =>
            Task.FromResult<WorkItem?>(workItem.Id == workItemId && workItem.TenantId == tenantId ? workItem : null);
        public Task<PagedResult<WorkItem>> ListByProjectAsync(
            Guid tenantId, Guid projectId, ProjectPermission permission, int skip, int take,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<WorkItem>([], 0));
        public Task<IReadOnlyList<WorkItem>> ListByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> workItemIds, ProjectPermission permission,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItem>>([]);
        public Task<bool> HasChildrenAsync(Guid tenantId, Guid parentWorkItemId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task RemoveAsync(WorkItem workItem, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TenantMembershipRepositoryStub(IReadOnlyList<TenantMembership> memberships)
        : ITenantMembershipRepository
    {
        public Task AddAsync(TenantMembership membership, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<TenantMembership?> GetActiveAsync(
            Guid tenantId, string issuer, string subject, CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);
        public Task<TenantMembership?> GetActiveByUserAsync(
            Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);
        public Task<TenantMembership?> GetActiveAsync(
            Guid tenantId, Guid membershipId, CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);
        public Task<TenantMembership?> GetOwnerAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);
        public Task<IReadOnlyList<TenantMembership>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(memberships);
        public Task<IReadOnlyList<TenantMembership>> ListByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> membershipIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>(
                memberships.Where(m => membershipIds.Contains(m.Id)).ToArray());
        public Task<IReadOnlyList<Guid>> ListActiveUserIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    private sealed class TeamMembershipRepositoryStub(IReadOnlyList<TeamMembership> memberships)
        : ITeamMembershipRepository
    {
        public Task AddAsync(TeamMembership membership, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RemoveAsync(TeamMembership membership, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<TeamMembership?> GetAsync(
            Guid tenantId, Guid teamId, Guid membershipId, CancellationToken cancellationToken) =>
            Task.FromResult<TeamMembership?>(null);
        public Task<IReadOnlyList<TeamMembership>> ListByTeamAsync(
            Guid tenantId, Guid teamId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TeamMembership>>(
                memberships.Where(m => m.TeamId == teamId).ToArray());
    }

    private sealed class SettingsRepositoryStub(IReadOnlyList<UserAccount> accounts) : ISettingsRepository
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

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
