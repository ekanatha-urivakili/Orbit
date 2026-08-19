using Orbit.Application.Abstractions;
using Orbit.Application.Boards;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Choices;
using Orbit.Domain.Common;
using Orbit.Domain.Identity;
using Orbit.Domain.Messaging;
using Orbit.Domain.Projects;
using Orbit.Domain.Settings;
using Orbit.Domain.WorkItems;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Tests;

public sealed class SprintHandlerTests
{
    private static WorkItem NewItem(Guid tenantId, Guid projectId, WorkItemStatus status, long sequenceNumber)
    {
        var item = WorkItem.Create(
            tenantId, projectId, sequenceNumber, "ORB", $"Card {sequenceNumber}", null,
            WorkItemType.Task, Priority.Medium, DateTimeOffset.UtcNow);
        if (status != WorkItemStatus.Backlog)
        {
            item.ChangeStatus(status, DateTimeOffset.UtcNow);
        }

        return item;
    }

    [Fact]
    public async Task CreateSprint_CreatesFutureSprint()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprints = new SprintRepositoryStub();
        var handler = new CreateSprintHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.TransitionWorkItem]),
            sprints,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(new CreateSprintCommand(project.Id, "Sprint 1"), CancellationToken.None);

        Assert.Equal("Sprint 1", result.Name);
        Assert.Equal(SprintState.Future, result.State);
        Assert.NotNull(sprints.Added);
    }

    [Fact]
    public async Task CreateSprint_HidesExistence_WhenPrincipalLacksPermission()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var handler = new CreateSprintHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.View]),
            new SprintRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(new CreateSprintCommand(project.Id, "Sprint 1"), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    [Fact]
    public async Task ListSprints_ReturnsCurrentMembershipWorkItemIds()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        var item = NewItem(tenantId, project.Id, WorkItemStatus.Backlog, 1);
        var membership = SprintMembership.Create(tenantId, sprint.Id, item.Id, DateTimeOffset.UtcNow);
        var memberships = new SprintMembershipRepositoryStub(membership);
        var handler = new ListSprintsHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.View]),
            new SprintRepositoryStub(sprint),
            memberships);

        var result = await handler.Handle(new ListSprintsQuery(project.Id), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal([item.Id], result[0].WorkItemIds);
        Assert.Equal(1, memberships.ListBySprintsCount);
        Assert.Equal(0, memberships.ListBySprintCount);
    }

    [Fact]
    public async Task StartSprint_TransitionsToActive()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        var handler = new StartSprintHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.TransitionWorkItem]),
            new SprintRepositoryStub(sprint),
            new SprintMembershipRepositoryStub(),
            new WorkItemRepositoryStub(),
            new CurrentPrincipalStub(null),
            new SettingsRepositoryStub([], []),
            new OutboxRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new StartSprintCommand(sprint.Id, "Goal", null, null, sprint.Version), CancellationToken.None);

        Assert.Equal(SprintState.Active, result.State);
        Assert.Equal("Goal", result.Goal);
    }

    [Fact]
    public async Task StartSprint_RejectsStaleVersion()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        var handler = new StartSprintHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.TransitionWorkItem]),
            new SprintRepositoryStub(sprint),
            new SprintMembershipRepositoryStub(),
            new WorkItemRepositoryStub(),
            new CurrentPrincipalStub(null),
            new SettingsRepositoryStub([], []),
            new OutboxRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new StartSprintCommand(sprint.Id, null, null, null, sprint.Version + 1), CancellationToken.None);

        await Assert.ThrowsAsync<ConcurrencyException>(action);
    }

    [Fact]
    public async Task StartSprint_RejectsWhenAnotherSprintAlreadyActive()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var activeSprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        activeSprint.Start(null, null, null, DateTimeOffset.UtcNow);
        var futureSprint = Sprint.Create(tenantId, project.Id, "Sprint 2", DateTimeOffset.UtcNow);
        var handler = new StartSprintHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.TransitionWorkItem]),
            new SprintRepositoryStub(activeSprint, futureSprint),
            new SprintMembershipRepositoryStub(),
            new WorkItemRepositoryStub(),
            new CurrentPrincipalStub(null),
            new SettingsRepositoryStub([], []),
            new OutboxRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new StartSprintCommand(futureSprint.Id, null, null, null, futureSprint.Version), CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(action);
    }

    [Fact]
    public async Task StartSprint_NotifiesMemberOwners()
    {
        var tenantId = Guid.NewGuid();
        var authorUserId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        var assigneeAccount = UserAccount.Create("assignee@example.com", "Assignee", DateTimeOffset.UtcNow);
        var item = NewItem(tenantId, project.Id, WorkItemStatus.Backlog, 1);
        item.SetDetails(
            parentId: null, epicName: null, acceptanceCriteria: null, stepsToConduct: null,
            assigneeUserId: assigneeAccount.Id, developerUserId: null, productOwnerUserId: null,
            sprintName: null, identifiedOn: null, startDate: null, teamId: null, storyPoints: null, labels: null, countries: null,
            attachmentNames: null);
        var membership = SprintMembership.Create(tenantId, sprint.Id, item.Id, DateTimeOffset.UtcNow);
        var outbox = new OutboxRepositoryStub();
        var handler = new StartSprintHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.TransitionWorkItem]),
            new SprintRepositoryStub(sprint),
            new SprintMembershipRepositoryStub(membership),
            new WorkItemRepositoryStub(item),
            new CurrentPrincipalStub(authorUserId),
            new SettingsRepositoryStub([assigneeAccount], []),
            outbox,
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(new StartSprintCommand(sprint.Id, null, null, null, sprint.Version), CancellationToken.None);

        var email = Assert.Single(outbox.Messages);
        Assert.Equal(assigneeAccount.NormalizedEmail, email.ToEmail);
        Assert.Contains(sprint.Name, email.Subject);
    }

    [Fact]
    public async Task CompleteSprint_MovesIncompleteItemsToBacklogAndKeepsDoneItems()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        sprint.Start(null, null, null, DateTimeOffset.UtcNow);
        var doneItem = NewItem(tenantId, project.Id, WorkItemStatus.Done, 1);
        var todoItem = NewItem(tenantId, project.Id, WorkItemStatus.InProgress, 2);
        var doneMembership = SprintMembership.Create(tenantId, sprint.Id, doneItem.Id, DateTimeOffset.UtcNow);
        var todoMembership = SprintMembership.Create(tenantId, sprint.Id, todoItem.Id, DateTimeOffset.UtcNow);
        var memberships = new SprintMembershipRepositoryStub(doneMembership, todoMembership);
        var facts = new SprintScopeFactRepositoryStub();
        var unitOfWork = new UnitOfWorkStub();
        var workItems = new WorkItemRepositoryStub(doneItem, todoItem);
        var handler = new CompleteSprintHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.TransitionWorkItem]),
            new SprintRepositoryStub(sprint),
            memberships,
            new SprintCompletionOperationRepositoryStub(),
            facts,
            workItems,
            new CurrentPrincipalStub(null),
            new SettingsRepositoryStub([], []),
            new OutboxRepositoryStub(),
            unitOfWork,
            TimeProvider.System);

        var result = await handler.Handle(
            new CompleteSprintCommand(sprint.Id, sprint.Version, null), CancellationToken.None);

        Assert.Equal(SprintState.Closed, result.State);
        Assert.Equal([doneItem.Id], result.WorkItemIds);
        Assert.NotNull(todoMembership.RemovedAt);
        Assert.Null(doneMembership.RemovedAt);
        Assert.Contains(facts.Added, fact => fact.FactType == AgileFactType.SprintRemoved && fact.WorkItemId == todoItem.Id);
        Assert.Contains(facts.Added, fact => fact.FactType == AgileFactType.SprintCompleted && fact.WorkItemId is null);
        Assert.Equal(1, workItems.ListByIdsCount);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CompleteSprint_NotifiesMemberOwners()
    {
        var tenantId = Guid.NewGuid();
        var authorUserId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        sprint.Start(null, null, null, DateTimeOffset.UtcNow);
        var assigneeAccount = UserAccount.Create("assignee@example.com", "Assignee", DateTimeOffset.UtcNow);
        var doneItem = NewItem(tenantId, project.Id, WorkItemStatus.Done, 1);
        doneItem.SetDetails(
            parentId: null, epicName: null, acceptanceCriteria: null, stepsToConduct: null,
            assigneeUserId: assigneeAccount.Id, developerUserId: null, productOwnerUserId: null,
            sprintName: null, identifiedOn: null, startDate: null, teamId: null, storyPoints: null, labels: null, countries: null,
            attachmentNames: null);
        var doneMembership = SprintMembership.Create(tenantId, sprint.Id, doneItem.Id, DateTimeOffset.UtcNow);
        var outbox = new OutboxRepositoryStub();
        var handler = new CompleteSprintHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.TransitionWorkItem]),
            new SprintRepositoryStub(sprint),
            new SprintMembershipRepositoryStub(doneMembership),
            new SprintCompletionOperationRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            new WorkItemRepositoryStub(doneItem),
            new CurrentPrincipalStub(authorUserId),
            new SettingsRepositoryStub([assigneeAccount], []),
            outbox,
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(new CompleteSprintCommand(sprint.Id, sprint.Version, null), CancellationToken.None);

        var email = Assert.Single(outbox.Messages);
        Assert.Equal(assigneeAccount.NormalizedEmail, email.ToEmail);
        Assert.Contains(sprint.Name, email.Subject);
    }

    [Fact]
    public async Task CompleteSprint_WithRolloverTarget_MovesIncompleteItemsIntoTargetSprint()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        sprint.Start(null, null, null, DateTimeOffset.UtcNow);
        var targetSprint = Sprint.Create(tenantId, project.Id, "Sprint 2", DateTimeOffset.UtcNow);
        var doneItem = NewItem(tenantId, project.Id, WorkItemStatus.Done, 1);
        var todoItem = NewItem(tenantId, project.Id, WorkItemStatus.InProgress, 2);
        var doneMembership = SprintMembership.Create(tenantId, sprint.Id, doneItem.Id, DateTimeOffset.UtcNow);
        var todoMembership = SprintMembership.Create(tenantId, sprint.Id, todoItem.Id, DateTimeOffset.UtcNow);
        var memberships = new SprintMembershipRepositoryStub(doneMembership, todoMembership);
        var handler = new CompleteSprintHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.TransitionWorkItem]),
            new SprintRepositoryStub(sprint, targetSprint),
            memberships,
            new SprintCompletionOperationRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            new WorkItemRepositoryStub(doneItem, todoItem),
            new CurrentPrincipalStub(null),
            new SettingsRepositoryStub([], []),
            new OutboxRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new CompleteSprintCommand(sprint.Id, sprint.Version, targetSprint.Id), CancellationToken.None);

        Assert.Equal(SprintState.Closed, result.State);
        Assert.NotNull(todoMembership.RemovedAt);
        var targetMembers = await memberships.ListCurrentBySprintAsync(tenantId, targetSprint.Id, CancellationToken.None);
        Assert.Equal([todoItem.Id], [.. targetMembers.Select(member => member.WorkItemId)]);
    }

    [Fact]
    public async Task CompleteSprint_RejectsRolloverTargetThatIsNotFuture()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        sprint.Start(null, null, null, DateTimeOffset.UtcNow);
        var targetSprint = Sprint.Create(tenantId, project.Id, "Sprint 2", DateTimeOffset.UtcNow);
        targetSprint.Start(null, null, null, DateTimeOffset.UtcNow);
        var handler = new CompleteSprintHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.TransitionWorkItem]),
            new SprintRepositoryStub(sprint, targetSprint),
            new SprintMembershipRepositoryStub(),
            new SprintCompletionOperationRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            new WorkItemRepositoryStub(),
            new CurrentPrincipalStub(null),
            new SettingsRepositoryStub([], []),
            new OutboxRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new CompleteSprintCommand(sprint.Id, sprint.Version, targetSprint.Id), CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(action);
    }

    [Fact]
    public async Task CompleteSprint_ResumesFromExistingOperation_WithoutReprocessingAlreadyRemovedItems()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        sprint.Start(null, null, null, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        sprint.StartClosing(now);
        var doneItem = NewItem(tenantId, project.Id, WorkItemStatus.Done, 1);
        var alreadyRemovedItem = NewItem(tenantId, project.Id, WorkItemStatus.InProgress, 2);
        var stillPendingItem = NewItem(tenantId, project.Id, WorkItemStatus.InProgress, 3);
        var doneMembership = SprintMembership.Create(tenantId, sprint.Id, doneItem.Id, now);
        var removedMembership = SprintMembership.Create(tenantId, sprint.Id, alreadyRemovedItem.Id, now);
        removedMembership.Remove(now);
        var pendingMembership = SprintMembership.Create(tenantId, sprint.Id, stillPendingItem.Id, now);
        var memberships = new SprintMembershipRepositoryStub(doneMembership, removedMembership, pendingMembership);
        var operation = SprintCompletionOperation.Create(tenantId, sprint.Id, null, totalCount: 3, now);
        operation.RecordProgress(1, now);
        var operations = new SprintCompletionOperationRepositoryStub(operation);
        var handler = new CompleteSprintHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.TransitionWorkItem]),
            new SprintRepositoryStub(sprint),
            memberships,
            operations,
            new SprintScopeFactRepositoryStub(),
            new WorkItemRepositoryStub(doneItem, alreadyRemovedItem, stillPendingItem),
            new CurrentPrincipalStub(null),
            new SettingsRepositoryStub([], []),
            new OutboxRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new CompleteSprintCommand(sprint.Id, sprint.Version, null), CancellationToken.None);

        Assert.Equal(SprintState.Closed, result.State);
        Assert.Equal([doneItem.Id], result.WorkItemIds);
        Assert.NotNull(pendingMembership.RemovedAt);
        Assert.Equal(3, operation.ProcessedCount);
        Assert.Equal(SprintCompletionOperationState.Completed, operation.State);
    }

    [Fact]
    public async Task CompleteSprint_IsIdempotent_WhenOperationAlreadyCompleted()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        sprint.Start(null, null, null, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        sprint.StartClosing(now);
        sprint.FinishClosing(now);
        var operation = SprintCompletionOperation.Create(tenantId, sprint.Id, null, totalCount: 0, now);
        operation.MarkCompleted(now);
        var handler = new CompleteSprintHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.TransitionWorkItem]),
            new SprintRepositoryStub(sprint),
            new SprintMembershipRepositoryStub(),
            new SprintCompletionOperationRepositoryStub(operation),
            new SprintScopeFactRepositoryStub(),
            new WorkItemRepositoryStub(),
            new CurrentPrincipalStub(null),
            new SettingsRepositoryStub([], []),
            new OutboxRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new CompleteSprintCommand(sprint.Id, sprint.Version + 100, null), CancellationToken.None);

        Assert.Equal(SprintState.Closed, result.State);
    }

    [Fact]
    public async Task ReopenSprint_TransitionsClosedSprintToReopened()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        sprint.Start(null, null, null, DateTimeOffset.UtcNow);
        sprint.StartClosing(DateTimeOffset.UtcNow);
        sprint.FinishClosing(DateTimeOffset.UtcNow);
        var facts = new SprintScopeFactRepositoryStub();
        var handler = new ReopenSprintHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.TransitionWorkItem]),
            new SprintRepositoryStub(sprint),
            new SprintMembershipRepositoryStub(),
            facts,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new ReopenSprintCommand(sprint.Id, sprint.Version), CancellationToken.None);

        Assert.Equal(SprintState.Reopened, result.State);
        Assert.Contains(facts.Added, fact => fact.FactType == AgileFactType.SprintReopened && fact.SprintId == sprint.Id);
    }

    [Fact]
    public async Task ReopenSprint_RejectsNonClosedSprint()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        var handler = new ReopenSprintHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.TransitionWorkItem]),
            new SprintRepositoryStub(sprint),
            new SprintMembershipRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new ReopenSprintCommand(sprint.Id, sprint.Version), CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(action);
    }

    [Fact]
    public async Task AssignWorkItemToSprint_MovesItemBetweenSprints()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sourceSprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        var targetSprint = Sprint.Create(tenantId, project.Id, "Sprint 2", DateTimeOffset.UtcNow);
        var item = NewItem(tenantId, project.Id, WorkItemStatus.Backlog, 1);
        var existingMembership = SprintMembership.Create(tenantId, sourceSprint.Id, item.Id, DateTimeOffset.UtcNow);
        var memberships = new SprintMembershipRepositoryStub(existingMembership);
        var facts = new SprintScopeFactRepositoryStub();
        var handler = new AssignWorkItemToSprintHandler(
            new TenantContextStub(tenantId),
            new WorkItemRepositoryStub(item),
            new SprintRepositoryStub(sourceSprint, targetSprint),
            memberships,
            facts,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new AssignWorkItemToSprintCommand(item.Id, targetSprint.Id), CancellationToken.None);

        Assert.Equal([item.Id], result.WorkItemIds);
        Assert.NotNull(existingMembership.RemovedAt);
        Assert.Contains(facts.Added, fact => fact.FactType == AgileFactType.SprintRemoved && fact.SprintId == sourceSprint.Id);
        Assert.Contains(facts.Added, fact => fact.FactType == AgileFactType.SprintAdded && fact.SprintId == targetSprint.Id);
    }

    [Fact]
    public async Task AssignWorkItemToSprint_RejectsClosedSprint()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        sprint.Start(null, null, null, DateTimeOffset.UtcNow);
        sprint.StartClosing(DateTimeOffset.UtcNow);
        sprint.FinishClosing(DateTimeOffset.UtcNow);
        var item = NewItem(tenantId, project.Id, WorkItemStatus.Backlog, 1);
        var handler = new AssignWorkItemToSprintHandler(
            new TenantContextStub(tenantId),
            new WorkItemRepositoryStub(item),
            new SprintRepositoryStub(sprint),
            new SprintMembershipRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new AssignWorkItemToSprintCommand(item.Id, sprint.Id), CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(action);
    }

    [Fact]
    public async Task AssignWorkItemToSprint_RejectsDifferentProject()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprint = Sprint.Create(tenantId, Guid.NewGuid(), "Sprint 1", DateTimeOffset.UtcNow);
        var item = NewItem(tenantId, project.Id, WorkItemStatus.Backlog, 1);
        var handler = new AssignWorkItemToSprintHandler(
            new TenantContextStub(tenantId),
            new WorkItemRepositoryStub(item),
            new SprintRepositoryStub(sprint),
            new SprintMembershipRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new AssignWorkItemToSprintCommand(item.Id, sprint.Id), CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(action);
    }

    [Fact]
    public async Task RemoveWorkItemFromSprint_RemovesCurrentMembership()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        var item = NewItem(tenantId, project.Id, WorkItemStatus.Backlog, 1);
        var membership = SprintMembership.Create(tenantId, sprint.Id, item.Id, DateTimeOffset.UtcNow);
        var facts = new SprintScopeFactRepositoryStub();
        var handler = new RemoveWorkItemFromSprintHandler(
            new TenantContextStub(tenantId),
            new WorkItemRepositoryStub(item),
            new SprintRepositoryStub(sprint),
            new SprintMembershipRepositoryStub(membership),
            facts,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(new RemoveWorkItemFromSprintCommand(item.Id), CancellationToken.None);

        Assert.Empty(result.WorkItemIds);
        Assert.NotNull(membership.RemovedAt);
        Assert.Contains(facts.Added, fact => fact.FactType == AgileFactType.SprintRemoved && fact.WorkItemId == item.Id);
    }

    [Fact]
    public async Task RemoveWorkItemFromSprint_ThrowsNotFound_WhenNoCurrentMembership()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var item = NewItem(tenantId, project.Id, WorkItemStatus.Backlog, 1);
        var handler = new RemoveWorkItemFromSprintHandler(
            new TenantContextStub(tenantId),
            new WorkItemRepositoryStub(item),
            new SprintRepositoryStub(),
            new SprintMembershipRepositoryStub(),
            new SprintScopeFactRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(new RemoveWorkItemFromSprintCommand(item.Id), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

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

    private sealed class WorkItemRepositoryStub(params WorkItem[] items) : IWorkItemRepository
    {
        public int ListByIdsCount { get; private set; }
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

        public Task<bool> HasChildrenAsync(Guid tenantId, Guid parentWorkItemId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task RemoveAsync(WorkItem workItem, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<WorkItem>> ListByIdsAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> workItemIds,
            ProjectPermission permission,
            CancellationToken cancellationToken)
        {
            ListByIdsCount++;
            return Task.FromResult<IReadOnlyList<WorkItem>>(
                items.Where(item => item.TenantId == tenantId && workItemIds.Contains(item.Id)).ToArray());
        }
    }

    private sealed class SprintRepositoryStub(params Sprint[] sprints) : ISprintRepository
    {
        private readonly List<Sprint> _sprints = [.. sprints];

        public Sprint? Added { get; private set; }

        public Task AddAsync(Sprint sprint, CancellationToken cancellationToken)
        {
            Added = sprint;
            _sprints.Add(sprint);
            return Task.CompletedTask;
        }

        public Task<Sprint?> GetAsync(Guid tenantId, Guid sprintId, CancellationToken cancellationToken) =>
            Task.FromResult(_sprints.SingleOrDefault(sprint => sprint.Id == sprintId && sprint.TenantId == tenantId));

        public Task<Sprint?> GetActiveAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(_sprints.SingleOrDefault(sprint =>
                sprint.TenantId == tenantId && sprint.ProjectId == projectId && sprint.State == SprintState.Active));

        public Task<IReadOnlyList<Sprint>> ListByProjectAsync(
            Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Sprint>>(
                [.. _sprints.Where(sprint => sprint.TenantId == tenantId && sprint.ProjectId == projectId)]);
    }

    private sealed class SprintMembershipRepositoryStub(params SprintMembership[] memberships) : ISprintMembershipRepository
    {
        private readonly List<SprintMembership> _memberships = [.. memberships];
        public int ListBySprintCount { get; private set; }
        public int ListBySprintsCount { get; private set; }

        public Task AddAsync(SprintMembership membership, CancellationToken cancellationToken)
        {
            _memberships.Add(membership);
            return Task.CompletedTask;
        }

        public Task<SprintMembership?> GetCurrentByWorkItemAsync(
            Guid tenantId, Guid workItemId, CancellationToken cancellationToken) =>
            Task.FromResult(_memberships.SingleOrDefault(membership =>
                membership.TenantId == tenantId && membership.WorkItemId == workItemId && membership.RemovedAt is null));

        public Task<IReadOnlyList<SprintMembership>> ListCurrentBySprintAsync(
            Guid tenantId, Guid sprintId, CancellationToken cancellationToken)
        {
            ListBySprintCount++;
            return Task.FromResult<IReadOnlyList<SprintMembership>>(
                [.. _memberships.Where(membership =>
                    membership.TenantId == tenantId && membership.SprintId == sprintId && membership.RemovedAt is null)]);
        }

        public Task<IReadOnlyList<SprintMembership>> ListCurrentBySprintsAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> sprintIds,
            CancellationToken cancellationToken)
        {
            ListBySprintsCount++;
            return Task.FromResult<IReadOnlyList<SprintMembership>>(
                [.. _memberships.Where(membership => membership.TenantId == tenantId
                    && sprintIds.Contains(membership.SprintId)
                    && membership.RemovedAt is null)]);
        }
    }

    private sealed class SprintCompletionOperationRepositoryStub(SprintCompletionOperation? existing = null)
        : ISprintCompletionOperationRepository
    {
        private readonly List<SprintCompletionOperation> _operations = existing is null ? [] : [existing];

        public Task AddAsync(SprintCompletionOperation operation, CancellationToken cancellationToken)
        {
            _operations.Add(operation);
            return Task.CompletedTask;
        }

        public Task<SprintCompletionOperation?> GetAsync(
            Guid tenantId, Guid sprintId, CancellationToken cancellationToken) =>
            Task.FromResult(_operations.SingleOrDefault(
                operation => operation.TenantId == tenantId && operation.SprintId == sprintId));
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
            Task.FromResult<IReadOnlyList<SprintScopeFact>>(
                [.. Added.Where(fact => fact.TenantId == tenantId && fact.SprintId == sprintId).OrderBy(fact => fact.OccurredAt)]);
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
