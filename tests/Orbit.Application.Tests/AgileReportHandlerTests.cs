using Orbit.Application.Abstractions;
using Orbit.Application.Boards;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Choices;
using Orbit.Domain.Configuration;
using Orbit.Domain.Projects;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.Tests;

public sealed class AgileReportHandlerTests
{
    private static DateTimeOffset AtNoon(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(12))), TimeSpan.Zero);

    private static WorkItemHistoryEntry StatusEntry(
        Guid tenantId, Guid workItemId, WorkItemStatusDefinition oldStatus, WorkItemStatusDefinition newStatus, DateTimeOffset at) =>
        WorkItemHistoryEntry.Create(
            tenantId, workItemId, Guid.NewGuid(), "Status", oldStatus.Key, newStatus.Key, at);

    private static IReadOnlyList<WorkItemStatusDefinition> DefaultStatuses(Guid tenantId, Guid projectId) =>
        WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, projectId, DateTimeOffset.UtcNow);

    private static WorkItemStatusDefinition Find(IReadOnlyList<WorkItemStatusDefinition> statuses, string key) =>
        statuses.Single(status => status.Key == key);

    [Fact]
    public async Task CumulativeFlowDiagram_ItemStaysBacklogUntilTransition_CountsShiftOnTransitionDay()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = DefaultStatuses(tenantId, project.Id);
        var backlog = Find(statuses, "backlog");
        var inProgress = Find(statuses, "in-progress");
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 1, 3);
        sprint.Start(null, start, end, DateTimeOffset.UtcNow);
        var workItemId = Guid.NewGuid();
        var middleDay = start.AddDays(1);

        var facts = new SprintScopeFactRepositoryStub(
            SprintScopeFact.Create(tenantId, sprint.Id, workItemId, AgileFactType.SprintAdded, 5m, AtNoon(start), AtNoon(start)));
        var history = new WorkItemHistoryRepositoryStub(
            StatusEntry(tenantId, workItemId, backlog, inProgress, AtNoon(middleDay)));

        var handler = new CumulativeFlowDiagramHandler(
            new TenantContextStub(tenantId), new ProjectRepositoryStub(project), new SprintRepositoryStub(sprint),
            facts, history, new WorkItemStatusRepositoryStub(statuses));

        var result = await handler.Handle(new CumulativeFlowDiagramQuery(sprint.Id), CancellationToken.None);

        Assert.Equal(3, result.Points.Count);
        Assert.Equal(1, result.Points[0].StatusCounts.Single(c => c.StatusId == backlog.Id).Count);
        Assert.Equal(0, result.Points[0].StatusCounts.Single(c => c.StatusId == inProgress.Id).Count);
        Assert.Equal(0, result.Points[1].StatusCounts.Single(c => c.StatusId == backlog.Id).Count);
        Assert.Equal(1, result.Points[1].StatusCounts.Single(c => c.StatusId == inProgress.Id).Count);
        Assert.Equal(1, result.Points[2].StatusCounts.Single(c => c.StatusId == inProgress.Id).Count);
    }

    [Fact]
    public async Task CumulativeFlowDiagram_ItemRemovedFromSprint_ExcludedAfterRemoval()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = DefaultStatuses(tenantId, project.Id);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 1, 3);
        sprint.Start(null, start, end, DateTimeOffset.UtcNow);
        var workItemId = Guid.NewGuid();
        var middleDay = start.AddDays(1);

        var facts = new SprintScopeFactRepositoryStub(
            SprintScopeFact.Create(tenantId, sprint.Id, workItemId, AgileFactType.SprintAdded, 5m, AtNoon(start), AtNoon(start)),
            SprintScopeFact.Create(tenantId, sprint.Id, workItemId, AgileFactType.SprintRemoved, -5m, AtNoon(middleDay), AtNoon(middleDay)));
        var history = new WorkItemHistoryRepositoryStub();

        var handler = new CumulativeFlowDiagramHandler(
            new TenantContextStub(tenantId), new ProjectRepositoryStub(project), new SprintRepositoryStub(sprint),
            facts, history, new WorkItemStatusRepositoryStub(statuses));

        var result = await handler.Handle(new CumulativeFlowDiagramQuery(sprint.Id), CancellationToken.None);

        Assert.Equal(1, result.Points[0].StatusCounts.Sum(c => c.Count));
        Assert.Equal(0, result.Points[1].StatusCounts.Sum(c => c.Count));
        Assert.Equal(0, result.Points[2].StatusCounts.Sum(c => c.Count));
    }

    [Fact]
    public async Task CumulativeFlowDiagram_RangeLongerThanAYear_ClampsTo366Days()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = DefaultStatuses(tenantId, project.Id);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        var start = new DateOnly(2026, 1, 1);
        var end = start.AddDays(1000);
        sprint.Start(null, start, end, DateTimeOffset.UtcNow);

        var handler = new CumulativeFlowDiagramHandler(
            new TenantContextStub(tenantId), new ProjectRepositoryStub(project), new SprintRepositoryStub(sprint),
            new SprintScopeFactRepositoryStub(), new WorkItemHistoryRepositoryStub(), new WorkItemStatusRepositoryStub(statuses));

        var result = await handler.Handle(new CumulativeFlowDiagramQuery(sprint.Id), CancellationToken.None);

        Assert.Equal(367, result.Points.Count);
    }

    [Fact]
    public async Task CycleTimeReport_ItemMovesInProgressThenDone_ComputesElapsedDays()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = DefaultStatuses(tenantId, project.Id);
        var backlog = Find(statuses, "backlog");
        var inProgress = Find(statuses, "in-progress");
        var done = Find(statuses, "done");
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 1, 5);
        sprint.Start(null, start, end, DateTimeOffset.UtcNow);
        var workItemId = Guid.NewGuid();
        var startedAt = AtNoon(start);
        var completedAt = AtNoon(start.AddDays(2));

        var facts = new SprintScopeFactRepositoryStub(
            SprintScopeFact.Create(tenantId, sprint.Id, workItemId, AgileFactType.SprintAdded, 5m, startedAt, startedAt));
        var history = new WorkItemHistoryRepositoryStub(
            StatusEntry(tenantId, workItemId, backlog, inProgress, startedAt),
            StatusEntry(tenantId, workItemId, inProgress, done, completedAt));

        var handler = new CycleTimeReportHandler(
            new TenantContextStub(tenantId), new ProjectRepositoryStub(project), new SprintRepositoryStub(sprint),
            facts, history, new WorkItemStatusRepositoryStub(statuses));

        var result = await handler.Handle(new CycleTimeReportQuery(sprint.Id), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(workItemId, item.WorkItemId);
        Assert.Equal(2m, item.CycleTimeDays);
        Assert.Equal(2m, result.AverageCycleTimeDays);
        Assert.Equal(2m, result.MedianCycleTimeDays);
    }

    [Fact]
    public async Task CycleTimeReport_ItemNeverReachedDone_ExcludedFromReport()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = DefaultStatuses(tenantId, project.Id);
        var backlog = Find(statuses, "backlog");
        var inProgress = Find(statuses, "in-progress");
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 1, 5);
        sprint.Start(null, start, end, DateTimeOffset.UtcNow);
        var workItemId = Guid.NewGuid();

        var facts = new SprintScopeFactRepositoryStub(
            SprintScopeFact.Create(tenantId, sprint.Id, workItemId, AgileFactType.SprintAdded, 5m, AtNoon(start), AtNoon(start)));
        var history = new WorkItemHistoryRepositoryStub(
            StatusEntry(tenantId, workItemId, backlog, inProgress, AtNoon(start)));

        var handler = new CycleTimeReportHandler(
            new TenantContextStub(tenantId), new ProjectRepositoryStub(project), new SprintRepositoryStub(sprint),
            facts, history, new WorkItemStatusRepositoryStub(statuses));

        var result = await handler.Handle(new CycleTimeReportQuery(sprint.Id), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Null(result.AverageCycleTimeDays);
        Assert.Null(result.MedianCycleTimeDays);
    }

    [Fact]
    public async Task ControlChart_MultipleCompletedItems_ReportsPointsAndPercentile()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = DefaultStatuses(tenantId, project.Id);
        var backlog = Find(statuses, "backlog");
        var inProgress = Find(statuses, "in-progress");
        var done = Find(statuses, "done");
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 1, 10);
        sprint.Start(null, start, end, DateTimeOffset.UtcNow);
        var fastItemId = Guid.NewGuid();
        var slowItemId = Guid.NewGuid();

        var facts = new SprintScopeFactRepositoryStub(
            SprintScopeFact.Create(tenantId, sprint.Id, fastItemId, AgileFactType.SprintAdded, 3m, AtNoon(start), AtNoon(start)),
            SprintScopeFact.Create(tenantId, sprint.Id, slowItemId, AgileFactType.SprintAdded, 5m, AtNoon(start), AtNoon(start)));
        var history = new WorkItemHistoryRepositoryStub(
            StatusEntry(tenantId, fastItemId, backlog, inProgress, AtNoon(start)),
            StatusEntry(tenantId, fastItemId, inProgress, done, AtNoon(start.AddDays(1))),
            StatusEntry(tenantId, slowItemId, backlog, inProgress, AtNoon(start)),
            StatusEntry(tenantId, slowItemId, inProgress, done, AtNoon(start.AddDays(5))));

        var handler = new ControlChartHandler(
            new TenantContextStub(tenantId), new ProjectRepositoryStub(project), new SprintRepositoryStub(sprint),
            facts, history, new WorkItemStatusRepositoryStub(statuses));

        var result = await handler.Handle(new ControlChartQuery(sprint.Id), CancellationToken.None);

        Assert.Equal(2, result.Points.Count);
        Assert.Equal(3m, result.AverageCycleTimeDays);
        Assert.NotNull(result.P85CycleTimeDays);
    }

    [Fact]
    public async Task CumulativeFlowDiagram_HidesExistence_WhenPrincipalCannotViewProject()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = DefaultStatuses(tenantId, project.Id);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        var handler = new CumulativeFlowDiagramHandler(
            new TenantContextStub(tenantId), new ProjectRepositoryStub(project, canView: false),
            new SprintRepositoryStub(sprint), new SprintScopeFactRepositoryStub(), new WorkItemHistoryRepositoryStub(),
            new WorkItemStatusRepositoryStub(statuses));

        var action = () => handler.Handle(new CumulativeFlowDiagramQuery(sprint.Id), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class ProjectRepositoryStub(Project project, bool canView = true) : IProjectRepository
    {
        public Task AddAsync(Project value, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Project?> GetAsync(
            Guid tenantId, Guid projectId, ProjectPermission permission, CancellationToken cancellationToken) =>
            Task.FromResult(project.Id == projectId && project.TenantId == tenantId && canView ? project : null);

        public Task<PagedResult<Project>> ListAsync(
            Guid tenantId, ProjectPermission permission, int skip, int take, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<Project>([project], 1));
    }

    private sealed class SprintRepositoryStub(params Sprint[] sprints) : ISprintRepository
    {
        public Task AddAsync(Sprint sprint, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Sprint?> GetAsync(Guid tenantId, Guid sprintId, CancellationToken cancellationToken) =>
            Task.FromResult(sprints.SingleOrDefault(sprint => sprint.Id == sprintId && sprint.TenantId == tenantId));

        public Task<Sprint?> GetActiveAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(sprints.SingleOrDefault(sprint =>
                sprint.TenantId == tenantId && sprint.ProjectId == projectId && sprint.State == SprintState.Active));

        public Task<IReadOnlyList<Sprint>> ListByProjectAsync(
            Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Sprint>>(
                [.. sprints.Where(sprint => sprint.TenantId == tenantId && sprint.ProjectId == projectId)]);
    }

    private sealed class SprintScopeFactRepositoryStub(params SprintScopeFact[] facts) : ISprintScopeFactRepository
    {
        public Task AddAsync(SprintScopeFact fact, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<SprintScopeFact>> ListBySprintAsync(
            Guid tenantId, Guid sprintId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SprintScopeFact>>(
                [.. facts.Where(fact => fact.TenantId == tenantId && fact.SprintId == sprintId).OrderBy(fact => fact.OccurredAt)]);
    }

    private sealed class WorkItemHistoryRepositoryStub(params WorkItemHistoryEntry[] entries) : IWorkItemHistoryRepository
    {
        public Task AddAsync(WorkItemHistoryEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<PagedResult<WorkItemHistoryEntry>> ListByWorkItemAsync(
            Guid tenantId, Guid workItemId, int skip, int take, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<WorkItemHistoryEntry>(
                [.. entries.Where(entry => entry.WorkItemId == workItemId)], entries.Length));

        public Task<IReadOnlyList<WorkItemHistoryEntry>> ListByWorkItemsAndFieldAsync(
            Guid tenantId, IReadOnlyCollection<Guid> workItemIds, string fieldName, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItemHistoryEntry>>(
                [.. entries.Where(entry => entry.TenantId == tenantId
                    && workItemIds.Contains(entry.WorkItemId) && entry.FieldName == fieldName)]);
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
                [.. statuses.Where(status => status.TenantId == tenantId && status.ProjectId == projectId)
                    .OrderBy(status => status.Order)]);

        public Task<WorkItemStatusDefinition?> GetDefaultAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(statuses
                .Where(status => status.TenantId == tenantId && status.ProjectId == projectId)
                .OrderBy(status => status.Order)
                .FirstOrDefault());

        public Task<bool> IsInUseAsync(Guid tenantId, Guid projectId, Guid statusId, string statusKey, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task RemoveAsync(WorkItemStatusDefinition definition, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
