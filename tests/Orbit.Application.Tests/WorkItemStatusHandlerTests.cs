using FluentValidation;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.Configuration;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Choices;
using Orbit.Domain.Configuration;
using Orbit.Domain.Projects;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.Tests;

public sealed class WorkItemStatusHandlerTests
{
    private static HybridCache CreateHybridCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    [Fact]
    public async Task ListWorkItemStatuses_ReturnsProjectCatalogInOrder()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, DateTimeOffset.UtcNow);
        var handler = new ListWorkItemStatusesHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.View]),
            new WorkItemStatusRepositoryStub(statuses),
            CreateHybridCache(),
            NullLogger<ListWorkItemStatusesHandler>.Instance);

        var result = await handler.Handle(new ListWorkItemStatusesQuery(project.Id), CancellationToken.None);

        Assert.Equal(6, result.Count);
        Assert.Equal("backlog", result[0].Key);
        Assert.Equal("blocked", result[^1].Key);
    }

    [Fact]
    public async Task ListWorkItemStatuses_ReflectsAWriteMadeAfterAnEarlierCachedRead()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = new WorkItemStatusRepositoryStub([]);
        var projects = new ProjectRepositoryStub(project, [ProjectPermission.View, ProjectPermission.Administer]);
        var cache = CreateHybridCache();
        var listHandler = new ListWorkItemStatusesHandler(
            new TenantContextStub(tenantId), projects, statuses, cache, NullLogger<ListWorkItemStatusesHandler>.Instance);
        var createHandler = new CreateWorkItemStatusHandler(
            new TenantContextStub(tenantId), projects, statuses, new BoardRepositoryStub(), new UnitOfWorkStub(),
            TimeProvider.System);

        var before = await listHandler.Handle(new ListWorkItemStatusesQuery(project.Id), CancellationToken.None);
        await createHandler.Handle(
            new CreateWorkItemStatusCommand(project.Id, "ready-for-qa", "Ready for QA", StatusCategory.InProgress, 45, "purple"),
            CancellationToken.None);
        var after = await listHandler.Handle(new ListWorkItemStatusesQuery(project.Id), CancellationToken.None);

        Assert.Empty(before);
        Assert.Contains(after, status => status.Key == "ready-for-qa");
    }

    [Fact]
    public async Task CreateWorkItemStatus_AddsCustomStatusToCatalog()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = new WorkItemStatusRepositoryStub([]);
        var handler = new CreateWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            statuses,
            new BoardRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new CreateWorkItemStatusCommand(project.Id, "ready-for-qa", "Ready for QA", StatusCategory.InProgress, 45, "purple"),
            CancellationToken.None);

        Assert.Equal("ready-for-qa", result.Key);
        Assert.False(result.IsSystem);
        Assert.NotNull(statuses.Added);
    }

    [Fact]
    public async Task CreateWorkItemStatus_AppendsBoardColumn_WhenBoardExists()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var seeded = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, DateTimeOffset.UtcNow);
        var backlog = seeded.Single(status => status.Key == "backlog");
        IReadOnlyList<BoardColumnInput> initialColumns = [new(backlog.Id, null, WipLimitMode.Warn)];
        var board = Board.Create(tenantId, project.Id, "Delivery Board", BoardType.Kanban, initialColumns, DateTimeOffset.UtcNow);
        var statuses = new WorkItemStatusRepositoryStub(seeded);
        var handler = new CreateWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            statuses,
            new BoardRepositoryStub(board),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new CreateWorkItemStatusCommand(project.Id, "ready-for-qa", "Ready for QA", StatusCategory.InProgress, 45, "purple"),
            CancellationToken.None);

        Assert.Equal(2, board.Columns.Count);
        Assert.Contains(board.Columns, column => column.StatusId == result.Id);
        Assert.Equal(2, board.Epoch);
    }

    [Fact]
    public async Task CreateWorkItemStatus_RejectsDuplicateKey()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var existing = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, DateTimeOffset.UtcNow);
        var handler = new CreateWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            new WorkItemStatusRepositoryStub(existing),
            new BoardRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new CreateWorkItemStatusCommand(project.Id, "backlog", "Backlog again", StatusCategory.ToDo, 5, "slate"),
            CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(action);
    }

    [Fact]
    public async Task UpdateWorkItemStatus_RenamesAndRecategorizes_WhenNeverUsed()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, DateTimeOffset.UtcNow);
        var blocked = statuses.Single(status => status.Key == "blocked");
        var handler = new UpdateWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            new WorkItemStatusRepositoryStub(statuses),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new UpdateWorkItemStatusCommand(project.Id, blocked.Id, "Blocked (urgent)", StatusCategory.ToDo, 5, "orange", blocked.Version),
            CancellationToken.None);

        Assert.Equal("Blocked (urgent)", result.Name);
        Assert.Equal(StatusCategory.ToDo, result.Category);
        Assert.Equal(2, result.Version);
    }

    [Fact]
    public async Task UpdateWorkItemStatus_AllowsPresentationChanges_WhenInUse()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, DateTimeOffset.UtcNow);
        var blocked = statuses.Single(status => status.Key == "blocked");
        var handler = new UpdateWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            new WorkItemStatusRepositoryStub(statuses, inUseStatusId: blocked.Id),
            new UnitOfWorkStub(),
            TimeProvider.System);

        // Category unchanged (still InProgress) - renaming/recoloring/reordering an in-use status is fine.
        var result = await handler.Handle(
            new UpdateWorkItemStatusCommand(project.Id, blocked.Id, "Blocked (urgent)", StatusCategory.InProgress, 5, "orange", blocked.Version),
            CancellationToken.None);

        Assert.Equal("Blocked (urgent)", result.Name);
    }

    [Fact]
    public async Task UpdateWorkItemStatus_RejectsRecategorization_WhenInUse()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, DateTimeOffset.UtcNow);
        var blocked = statuses.Single(status => status.Key == "blocked");
        var handler = new UpdateWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            new WorkItemStatusRepositoryStub(statuses, inUseStatusId: blocked.Id),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new UpdateWorkItemStatusCommand(project.Id, blocked.Id, "Blocked", StatusCategory.Done, 5, "orange", blocked.Version),
            CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(action);
    }

    [Fact]
    public async Task SetDefaultWorkItemStatus_MovesDefaultFlagBetweenStatuses()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, DateTimeOffset.UtcNow);
        var backlog = statuses.Single(status => status.Key == "backlog");
        var selected = statuses.Single(status => status.Key == "selected");
        var handler = new SetDefaultWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            new WorkItemStatusRepositoryStub(statuses),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(new SetDefaultWorkItemStatusCommand(project.Id, selected.Id), CancellationToken.None);

        Assert.True(result.IsDefault);
        Assert.False(backlog.IsDefault);
        Assert.True(selected.IsDefault);
    }

    [Fact]
    public async Task DeleteWorkItemStatus_RejectsWhenOnlyOneStatusRemains()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var lone = WorkItemStatusDefinition.Create(
            tenantId, project.Id, "backlog", "Backlog", StatusCategory.ToDo, 10, "slate", DateTimeOffset.UtcNow);
        var handler = new DeleteWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            new WorkItemStatusRepositoryStub([lone]),
            new UnitOfWorkStub());

        var action = () => handler.Handle(new DeleteWorkItemStatusCommand(project.Id, lone.Id), CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(action);
    }

    [Fact]
    public async Task DeleteWorkItemStatus_RejectsWhenInUse()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, DateTimeOffset.UtcNow);
        var backlog = statuses.Single(status => status.Key == "backlog");
        var handler = new DeleteWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            new WorkItemStatusRepositoryStub(statuses, inUseStatusId: backlog.Id),
            new UnitOfWorkStub());

        var action = () => handler.Handle(new DeleteWorkItemStatusCommand(project.Id, backlog.Id), CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(action);
    }

    [Fact]
    public async Task DeleteWorkItemStatus_RejectsDefaultStatus()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, DateTimeOffset.UtcNow);
        var backlog = statuses.Single(status => status.Key == "backlog");
        var handler = new DeleteWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            new WorkItemStatusRepositoryStub(statuses),
            new UnitOfWorkStub());

        var action = () => handler.Handle(new DeleteWorkItemStatusCommand(project.Id, backlog.Id), CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(action);
    }

    [Fact]
    public async Task DeleteWorkItemStatus_RemovesUnusedCustomStatus()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, DateTimeOffset.UtcNow);
        var blocked = statuses.Single(status => status.Key == "blocked");
        var repository = new WorkItemStatusRepositoryStub(statuses);
        var handler = new DeleteWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            repository,
            new UnitOfWorkStub());

        await handler.Handle(new DeleteWorkItemStatusCommand(project.Id, blocked.Id), CancellationToken.None);

        Assert.Equal(blocked.Id, repository.Removed?.Id);
    }

    [Fact]
    public async Task CreateWorkItemStatus_IncrementsProjectConfigEpoch()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var handler = new CreateWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            new WorkItemStatusRepositoryStub([]),
            new BoardRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(
            new CreateWorkItemStatusCommand(project.Id, "ready-for-qa", "Ready for QA", StatusCategory.InProgress, 45, "purple"),
            CancellationToken.None);

        Assert.Equal(2, project.ConfigEpoch);
    }

    [Fact]
    public async Task UpdateWorkItemStatus_IncrementsProjectConfigEpoch()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, DateTimeOffset.UtcNow);
        var blocked = statuses.Single(status => status.Key == "blocked");
        var handler = new UpdateWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            new WorkItemStatusRepositoryStub(statuses),
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(
            new UpdateWorkItemStatusCommand(project.Id, blocked.Id, "Blocked (urgent)", StatusCategory.ToDo, 5, "orange", blocked.Version),
            CancellationToken.None);

        Assert.Equal(2, project.ConfigEpoch);
    }

    [Fact]
    public async Task SetDefaultWorkItemStatus_IncrementsProjectConfigEpoch()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, DateTimeOffset.UtcNow);
        var selected = statuses.Single(status => status.Key == "selected");
        var handler = new SetDefaultWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            new WorkItemStatusRepositoryStub(statuses),
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(new SetDefaultWorkItemStatusCommand(project.Id, selected.Id), CancellationToken.None);

        Assert.Equal(2, project.ConfigEpoch);
    }

    [Fact]
    public async Task DeleteWorkItemStatus_IncrementsProjectConfigEpoch()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, DateTimeOffset.UtcNow);
        var blocked = statuses.Single(status => status.Key == "blocked");
        var handler = new DeleteWorkItemStatusHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            new WorkItemStatusRepositoryStub(statuses),
            new UnitOfWorkStub());

        await handler.Handle(new DeleteWorkItemStatusCommand(project.Id, blocked.Id), CancellationToken.None);

        Assert.Equal(2, project.ConfigEpoch);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class ProjectRepositoryStub(Project project, ProjectPermission[] allowedPermissions) : IProjectRepository
    {
        public Task AddAsync(Project value, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Project?> GetAsync(
            Guid tenantId, Guid projectId, ProjectPermission permission, CancellationToken cancellationToken) =>
            Task.FromResult(
                project.Id == projectId && project.TenantId == tenantId && allowedPermissions.Contains(permission)
                    ? project
                    : null);

        public Task<PagedResult<Project>> ListAsync(
            Guid tenantId, ProjectPermission permission, int skip, int take, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<Project>([project], 1));
    }

    private sealed class WorkItemStatusRepositoryStub(
        IReadOnlyList<WorkItemStatusDefinition> statuses, Guid? inUseStatusId = null) : IWorkItemStatusRepository
    {
        private readonly List<WorkItemStatusDefinition> _statuses = [.. statuses];

        public WorkItemStatusDefinition? Added { get; private set; }
        public WorkItemStatusDefinition? Removed { get; private set; }

        public Task AddAsync(WorkItemStatusDefinition definition, CancellationToken cancellationToken)
        {
            Added = definition;
            _statuses.Add(definition);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IReadOnlyCollection<WorkItemStatusDefinition> definitions, CancellationToken cancellationToken)
        {
            _statuses.AddRange(definitions);
            return Task.CompletedTask;
        }

        public Task<WorkItemStatusDefinition?> GetAsync(
            Guid tenantId, Guid projectId, Guid statusId, CancellationToken cancellationToken) =>
            Task.FromResult(_statuses.SingleOrDefault(status => status.Id == statusId));

        public Task<IReadOnlyList<WorkItemStatusDefinition>> ListByProjectAsync(
            Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItemStatusDefinition>>([.. _statuses.OrderBy(status => status.Order)]);

        public Task<WorkItemStatusDefinition?> GetDefaultAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(
                _statuses.SingleOrDefault(status => status.IsDefault)
                ?? _statuses.OrderBy(status => status.Order).FirstOrDefault());

        public Task<bool> IsInUseAsync(
            Guid tenantId, Guid projectId, Guid statusId, string statusKey, CancellationToken cancellationToken) =>
            Task.FromResult(inUseStatusId == statusId);

        public Task RemoveAsync(WorkItemStatusDefinition definition, CancellationToken cancellationToken)
        {
            Removed = definition;
            _statuses.RemoveAll(status => status.Id == definition.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class BoardRepositoryStub(Board? existing = null) : IBoardRepository
    {
        public Task AddAsync(Board board, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Board?> GetAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(existing);
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
