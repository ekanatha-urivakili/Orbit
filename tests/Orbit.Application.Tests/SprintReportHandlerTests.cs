using Orbit.Application.Abstractions;
using Orbit.Application.Boards;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Choices;
using Orbit.Domain.Projects;

namespace Orbit.Application.Tests;

public sealed class SprintReportHandlerTests
{
    private static DateTimeOffset AtNoon(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(12))), TimeSpan.Zero);

    [Fact]
    public async Task Handle_NoScopeChangesAfterStart_BurndownStaysFlatAtCommittedPoints()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 1, 3);
        sprint.Start(null, start, end, DateTimeOffset.UtcNow);
        var workItemId = Guid.NewGuid();

        var facts = new SprintScopeFactRepositoryStub(
            SprintScopeFact.Create(tenantId, sprint.Id, workItemId, AgileFactType.SprintAdded, 5m, AtNoon(start), AtNoon(start)));
        var handler = new SprintReportHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project),
            new SprintRepositoryStub(sprint),
            facts);

        var result = await handler.Handle(new SprintReportQuery(sprint.Id), CancellationToken.None);

        Assert.Equal(5m, result.CommittedPoints);
        Assert.Equal(0m, result.AddedAfterStartPoints);
        Assert.Equal(0m, result.RemovedAfterStartPoints);
        Assert.Equal(0m, result.CompletedPoints);
        Assert.Equal(3, result.Burndown.Count);
        Assert.All(result.Burndown, point => Assert.Equal(5m, point.RemainingPoints));
    }

    [Fact]
    public async Task Handle_MidSprintEstimateChange_ShiftsBurndownFromThatDayOnward()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 1, 3);
        sprint.Start(null, start, end, DateTimeOffset.UtcNow);
        var workItemId = Guid.NewGuid();
        var middleDay = start.AddDays(1);

        var facts = new SprintScopeFactRepositoryStub(
            SprintScopeFact.Create(tenantId, sprint.Id, workItemId, AgileFactType.SprintAdded, 5m, AtNoon(start), AtNoon(start)),
            SprintScopeFact.Create(tenantId, sprint.Id, workItemId, AgileFactType.EstimateChanged, 3m, AtNoon(middleDay), AtNoon(middleDay)));
        var handler = new SprintReportHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project),
            new SprintRepositoryStub(sprint),
            facts);

        var result = await handler.Handle(new SprintReportQuery(sprint.Id), CancellationToken.None);

        Assert.Equal(5m, result.CommittedPoints);
        Assert.Equal(5m, result.Burndown[0].RemainingPoints);
        Assert.Equal(8m, result.Burndown[1].RemainingPoints);
        Assert.Equal(8m, result.Burndown[2].RemainingPoints);
    }

    [Fact]
    public async Task Handle_ItemRemovedMidSprint_ReducesBurndownAndReportsScopeChange()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 1, 3);
        sprint.Start(null, start, end, DateTimeOffset.UtcNow);
        var workItemId = Guid.NewGuid();
        var middleDay = start.AddDays(1);

        var facts = new SprintScopeFactRepositoryStub(
            SprintScopeFact.Create(tenantId, sprint.Id, workItemId, AgileFactType.SprintAdded, 5m, AtNoon(start), AtNoon(start)),
            SprintScopeFact.Create(tenantId, sprint.Id, workItemId, AgileFactType.SprintRemoved, -5m, AtNoon(middleDay), AtNoon(middleDay)));
        var handler = new SprintReportHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project),
            new SprintRepositoryStub(sprint),
            facts);

        var result = await handler.Handle(new SprintReportQuery(sprint.Id), CancellationToken.None);

        Assert.Equal(5m, result.CommittedPoints);
        Assert.Equal(5m, result.RemovedAfterStartPoints);
        Assert.Equal(5m, result.Burndown[0].RemainingPoints);
        Assert.Equal(0m, result.Burndown[1].RemainingPoints);
        Assert.Equal(0m, result.Burndown[2].RemainingPoints);
        Assert.Contains(result.ScopeChanges, change => change.FactType == AgileFactType.SprintRemoved && change.WorkItemId == workItemId);
    }

    [Fact]
    public async Task Handle_ClosedSprint_ReportIsStableFromImmutableFacts()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 1, 2);
        sprint.Start(null, start, end, DateTimeOffset.UtcNow);
        var doneItemId = Guid.NewGuid();
        var facts = new SprintScopeFactRepositoryStub(
            SprintScopeFact.Create(tenantId, sprint.Id, doneItemId, AgileFactType.SprintAdded, 8m, AtNoon(start), AtNoon(start)),
            SprintScopeFact.Create(tenantId, sprint.Id, doneItemId, AgileFactType.StatusChanged, -8m, AtNoon(end), AtNoon(end)));
        sprint.StartClosing(DateTimeOffset.UtcNow);
        sprint.FinishClosing(DateTimeOffset.UtcNow);
        var handler = new SprintReportHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project),
            new SprintRepositoryStub(sprint),
            facts);

        var result = await handler.Handle(new SprintReportQuery(sprint.Id), CancellationToken.None);

        Assert.Equal(SprintState.Closed, result.State);
        Assert.Equal(8m, result.CommittedPoints);
        Assert.Equal(8m, result.CompletedPoints);
        Assert.Equal(0m, result.Burndown[^1].RemainingPoints);
    }

    [Fact]
    public async Task Handle_HidesExistence_WhenPrincipalCannotViewProject()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var sprint = Sprint.Create(tenantId, project.Id, "Sprint 1", DateTimeOffset.UtcNow);
        var handler = new SprintReportHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, canView: false),
            new SprintRepositoryStub(sprint),
            new SprintScopeFactRepositoryStub());

        var action = () => handler.Handle(new SprintReportQuery(sprint.Id), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class ProjectRepositoryStub(Project project, bool canView = true) : IProjectRepository
    {
        public Task AddAsync(Project value, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Project?> GetAsync(
            Guid tenantId,
            Guid projectId,
            ProjectPermission permission,
            CancellationToken cancellationToken) =>
            Task.FromResult(project.Id == projectId && project.TenantId == tenantId && canView ? project : null);

        public Task<PagedResult<Project>> ListAsync(
            Guid tenantId,
            ProjectPermission permission,
            int skip,
            int take,
            CancellationToken cancellationToken) =>
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
}
