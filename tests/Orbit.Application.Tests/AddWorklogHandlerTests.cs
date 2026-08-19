using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.WorkItems;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.Identity;
using Orbit.Domain.WorkItems;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Tests;

public sealed class AddWorklogHandlerTests
{
    [Fact]
    public async Task Handle_PersistsWorklogUnderCallerMembership()
    {
        var tenantId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var workItem = WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Log work on this card", null, WorkItemType.Task, Priority.Medium,
            DateTimeOffset.UtcNow);
        var worklogs = new WorkItemWorklogRepositoryStub();
        var handler = new AddWorklogHandler(
            new TenantContextStub(tenantId), new CurrentPrincipalStub(membershipId),
            new WorkItemRepositoryStub(workItem), worklogs, new UnitOfWorkStub(), TimeProvider.System);

        var result = await handler.Handle(
            new AddWorklogCommand(workItem.Id, 45, DateOnly.FromDateTime(DateTime.UtcNow), "Investigated"),
            CancellationToken.None);

        Assert.Equal(membershipId, result.AuthorMembershipId);
        Assert.Equal(45, result.MinutesSpent);
        Assert.Single(worklogs.Items);
    }

    [Fact]
    public async Task DeleteHandle_OnlyAuthorCanDelete()
    {
        var tenantId = Guid.NewGuid();
        var authorMembershipId = Guid.NewGuid();
        var workItem = WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Log work on this card", null, WorkItemType.Task, Priority.Medium,
            DateTimeOffset.UtcNow);
        var worklog = WorkItemWorklog.Create(
            tenantId, workItem.Id, authorMembershipId, 30, DateOnly.FromDateTime(DateTime.UtcNow), null,
            DateTimeOffset.UtcNow);
        var worklogs = new WorkItemWorklogRepositoryStub(worklog);
        var otherHandler = new DeleteWorklogHandler(
            new TenantContextStub(tenantId), new CurrentPrincipalStub(Guid.NewGuid()),
            new WorkItemRepositoryStub(workItem), worklogs, new UnitOfWorkStub());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            otherHandler.Handle(new DeleteWorklogCommand(worklog.WorkItemId, worklog.Id), CancellationToken.None));

        var authorHandler = new DeleteWorklogHandler(
            new TenantContextStub(tenantId), new CurrentPrincipalStub(authorMembershipId),
            new WorkItemRepositoryStub(workItem), worklogs, new UnitOfWorkStub());
        await authorHandler.Handle(new DeleteWorklogCommand(worklog.WorkItemId, worklog.Id), CancellationToken.None);

        Assert.Empty(worklogs.Items);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class CurrentPrincipalStub(Guid membershipId) : ICurrentPrincipal
    {
        public Guid? UserId => Guid.NewGuid();
        public Guid? SessionId => null;
        public Guid MembershipId => membershipId;
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

    private sealed class WorkItemWorklogRepositoryStub(params WorkItemWorklog[] initial) : IWorkItemWorklogRepository
    {
        public List<WorkItemWorklog> Items { get; } = [.. initial];

        public Task AddAsync(WorkItemWorklog worklog, CancellationToken cancellationToken)
        {
            Items.Add(worklog);
            return Task.CompletedTask;
        }

        public Task<WorkItemWorklog?> GetAsync(Guid tenantId, Guid worklogId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(worklog => worklog.TenantId == tenantId && worklog.Id == worklogId));

        public Task<PagedResult<WorkItemWorklog>> ListByWorkItemAsync(
            Guid tenantId, Guid workItemId, int skip, int take, CancellationToken cancellationToken)
        {
            var matches = Items.Where(worklog => worklog.TenantId == tenantId && worklog.WorkItemId == workItemId).ToArray();
            return Task.FromResult(new PagedResult<WorkItemWorklog>(matches, matches.Length));
        }

        public Task RemoveAsync(WorkItemWorklog worklog, CancellationToken cancellationToken)
        {
            Items.Remove(worklog);
            return Task.CompletedTask;
        }
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
