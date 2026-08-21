using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.WorkItems;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.Identity;
using Orbit.Domain.WorkItems;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Tests;

public sealed class WorkItemVoteHandlerTests
{
    [Fact]
    public async Task AddVote_ThenGetVotes_ReflectsCallerVoteAndCount()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var workItem = CreateWorkItem(tenantId);
        var votes = new WorkItemVoteRepositoryStub();
        var addHandler = new AddWorkItemVoteHandler(
            new TenantContextStub(tenantId), new CurrentPrincipalStub(userId), new WorkItemRepositoryStub(workItem),
            votes, new UnitOfWorkStub(), TimeProvider.System);

        await addHandler.Handle(new AddWorkItemVoteCommand(workItem.Id), CancellationToken.None);
        await addHandler.Handle(new AddWorkItemVoteCommand(workItem.Id), CancellationToken.None);

        var getHandler = new GetWorkItemVotesHandler(
            new TenantContextStub(tenantId), new CurrentPrincipalStub(userId), new WorkItemRepositoryStub(workItem), votes);
        var result = await getHandler.Handle(new GetWorkItemVotesQuery(workItem.Id), CancellationToken.None);

        Assert.True(result.HasVoted);
        Assert.Equal(1, result.Count);
    }

    [Fact]
    public async Task RemoveVote_ClearsCallerVote()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var workItem = CreateWorkItem(tenantId);
        var votes = new WorkItemVoteRepositoryStub();
        var addHandler = new AddWorkItemVoteHandler(
            new TenantContextStub(tenantId), new CurrentPrincipalStub(userId), new WorkItemRepositoryStub(workItem),
            votes, new UnitOfWorkStub(), TimeProvider.System);
        await addHandler.Handle(new AddWorkItemVoteCommand(workItem.Id), CancellationToken.None);

        var removeHandler = new RemoveWorkItemVoteHandler(
            new TenantContextStub(tenantId), new CurrentPrincipalStub(userId), new WorkItemRepositoryStub(workItem),
            votes, new UnitOfWorkStub());
        await removeHandler.Handle(new RemoveWorkItemVoteCommand(workItem.Id), CancellationToken.None);

        var getHandler = new GetWorkItemVotesHandler(
            new TenantContextStub(tenantId), new CurrentPrincipalStub(userId), new WorkItemRepositoryStub(workItem), votes);
        var result = await getHandler.Handle(new GetWorkItemVotesQuery(workItem.Id), CancellationToken.None);

        Assert.False(result.HasVoted);
        Assert.Equal(0, result.Count);
    }

    private static WorkItem CreateWorkItem(Guid tenantId) =>
        WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Vote for this card", null, WorkItemType.Story, Priority.High,
            Guid.NewGuid(), DateTimeOffset.UtcNow);

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

    private sealed class WorkItemVoteRepositoryStub : IWorkItemVoteRepository
    {
        private readonly List<WorkItemVote> votes = [];

        public Task AddAsync(WorkItemVote vote, CancellationToken cancellationToken)
        {
            votes.Add(vote);
            return Task.CompletedTask;
        }

        public Task<WorkItemVote?> GetAsync(
            Guid tenantId, Guid workItemId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(votes.SingleOrDefault(
                vote => vote.TenantId == tenantId && vote.WorkItemId == workItemId && vote.UserId == userId));

        public Task<IReadOnlyList<WorkItemVote>> ListByWorkItemAsync(
            Guid tenantId, Guid workItemId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItemVote>>(
                votes.Where(vote => vote.TenantId == tenantId && vote.WorkItemId == workItemId).ToArray());

        public Task RemoveAsync(WorkItemVote vote, CancellationToken cancellationToken)
        {
            votes.Remove(vote);
            return Task.CompletedTask;
        }
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
