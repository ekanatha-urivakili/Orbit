using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Application.Abstractions;
using Orbit.Application.Boards;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Choices;
using Orbit.Domain.Configuration;
using Orbit.Domain.Projects;

namespace Orbit.Application.Tests;

public sealed class BoardHandlerTests
{
    private static HybridCache CreateHybridCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    [Fact]
    public async Task GetBoard_ReturnsZeroVersionSentinel_WhenNoBoardExists()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, DateTimeOffset.UtcNow);
        var handler = new GetBoardHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.View]),
            new BoardRepositoryStub(),
            new WorkItemStatusRepositoryStub(statuses),
            CreateHybridCache(),
            NullLogger<GetBoardHandler>.Instance);

        var result = await handler.Handle(new GetBoardQuery(project.Id), CancellationToken.None);

        Assert.Equal(0, result.Version);
    }

    [Fact]
    public async Task GetBoard_ReflectsAWriteMadeAfterAnEarlierCachedRead()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, DateTimeOffset.UtcNow);
        var backlog = statuses.Single(status => status.Key == "backlog");
        var existing = Board.Create(
            tenantId, project.Id, "Delivery Board", BoardType.Kanban,
            [new BoardColumnInput(backlog.Id, null, WipLimitMode.Warn)], DateTimeOffset.UtcNow);
        var boards = new BoardRepositoryStub { Existing = existing };
        var cache = CreateHybridCache();
        var getHandler = new GetBoardHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.View, ProjectPermission.Administer]),
            boards,
            new WorkItemStatusRepositoryStub(statuses),
            cache,
            NullLogger<GetBoardHandler>.Instance);
        var updateHandler = new UpdateBoardHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.View, ProjectPermission.Administer]),
            boards,
            new WorkItemStatusRepositoryStub(statuses),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var before = await getHandler.Handle(new GetBoardQuery(project.Id), CancellationToken.None);
        await updateHandler.Handle(
            new UpdateBoardCommand(project.Id, "Renamed Board", BoardType.Scrum, [], existing.Version),
            CancellationToken.None);
        var after = await getHandler.Handle(new GetBoardQuery(project.Id), CancellationToken.None);

        Assert.Equal("Delivery Board", before.Name);
        Assert.Equal("Renamed Board", after.Name);
    }

    [Fact]
    public async Task UpdateBoard_CreatesBoard_OnFirstUpdate()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, DateTimeOffset.UtcNow);
        var boards = new BoardRepositoryStub();
        var handler = new UpdateBoardHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            boards,
            new WorkItemStatusRepositoryStub(statuses),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new UpdateBoardCommand(project.Id, "Delivery Board", BoardType.Kanban, [], 0),
            CancellationToken.None);

        Assert.Equal("Delivery Board", result.Name);
        Assert.Equal(1, result.Version);
        Assert.NotNull(boards.Added);
        Assert.Equal(tenantId, boards.Added!.TenantId);
        Assert.Equal(statuses.Count, result.Columns.Count);
    }

    [Fact]
    public async Task UpdateBoard_AppliesRequestedColumnsAndWipLimits()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, DateTimeOffset.UtcNow);
        var backlog = statuses.Single(status => status.Key == "backlog");
        var inProgress = statuses.Single(status => status.Key == "in-progress");
        var boards = new BoardRepositoryStub();
        var handler = new UpdateBoardHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            boards,
            new WorkItemStatusRepositoryStub(statuses),
            new UnitOfWorkStub(),
            TimeProvider.System);

        IReadOnlyList<UpdateBoardColumnInput> requestedColumns =
        [
            new(backlog.Id, null, WipLimitMode.Warn),
            new(inProgress.Id, 3, WipLimitMode.Block),
        ];

        var result = await handler.Handle(
            new UpdateBoardCommand(project.Id, "Delivery Board", BoardType.Kanban, requestedColumns, 0),
            CancellationToken.None);

        Assert.Equal(2, result.Columns.Count);
        Assert.Equal(inProgress.Id, result.Columns[1].StatusId);
        Assert.Equal(3, result.Columns[1].WipLimit);
        Assert.Equal(WipLimitMode.Block, result.Columns[1].WipLimitMode);
    }

    [Fact]
    public async Task UpdateBoard_PreservesExistingColumns_WhenNoneRequested()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, DateTimeOffset.UtcNow);
        var done = statuses.Single(status => status.Key == "done");
        IReadOnlyList<BoardColumnInput> initialColumns = [new(done.Id, 2, WipLimitMode.Block)];
        var existing = Board.Create(tenantId, project.Id, "Delivery Board", BoardType.Kanban, initialColumns, DateTimeOffset.UtcNow);
        var handler = new UpdateBoardHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            new BoardRepositoryStub { Existing = existing },
            new WorkItemStatusRepositoryStub(statuses),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new UpdateBoardCommand(project.Id, "Renamed", BoardType.Scrum, [], 1),
            CancellationToken.None);

        Assert.Single(result.Columns);
        Assert.Equal(done.Id, result.Columns[0].StatusId);
        Assert.Equal(2, result.Columns[0].WipLimit);
    }

    [Fact]
    public async Task UpdateBoard_RejectsStaleVersion()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, DateTimeOffset.UtcNow);
        var backlog = statuses.Single(status => status.Key == "backlog");
        IReadOnlyList<BoardColumnInput> columns = [new(backlog.Id, null, WipLimitMode.Warn)];
        var existing = Board.Create(tenantId, project.Id, "Delivery Board", BoardType.Kanban, columns, DateTimeOffset.UtcNow);
        var handler = new UpdateBoardHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            new BoardRepositoryStub { Existing = existing },
            new WorkItemStatusRepositoryStub(statuses),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new UpdateBoardCommand(project.Id, "Renamed", BoardType.Scrum, [], 5),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConcurrencyException>(action);
    }

    [Fact]
    public async Task UpdateBoard_HidesExistence_WhenPrincipalLacksAdministerPermission()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var statuses = WorkItemStatusDefinition.CreateSoftwareDefaults(tenantId, project.Id, DateTimeOffset.UtcNow);
        var handler = new UpdateBoardHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.View]),
            new BoardRepositoryStub(),
            new WorkItemStatusRepositoryStub(statuses),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new UpdateBoardCommand(project.Id, "Delivery Board", BoardType.Kanban, [], 0),
            CancellationToken.None);

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

    private sealed class BoardRepositoryStub : IBoardRepository
    {
        public Board? Added { get; private set; }
        public Board? Existing { get; set; }

        public Task AddAsync(Board board, CancellationToken cancellationToken)
        {
            Added = board;
            return Task.CompletedTask;
        }

        public Task<Board?> GetAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(Existing?.ProjectId == projectId && Existing.TenantId == tenantId ? Existing : null);
    }

    private sealed class WorkItemStatusRepositoryStub(IReadOnlyList<WorkItemStatusDefinition> statuses) : IWorkItemStatusRepository
    {
        public Task AddAsync(WorkItemStatusDefinition definition, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddRangeAsync(IReadOnlyCollection<WorkItemStatusDefinition> definitions, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<WorkItemStatusDefinition?> GetAsync(
            Guid tenantId, Guid projectId, Guid statusId, CancellationToken cancellationToken) =>
            Task.FromResult(statuses.SingleOrDefault(status => status.Id == statusId));

        public Task<IReadOnlyList<WorkItemStatusDefinition>> ListByProjectAsync(
            Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(statuses);

        public Task<WorkItemStatusDefinition?> GetDefaultAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<WorkItemStatusDefinition?>(statuses.OrderBy(status => status.Order).First());

        public Task<bool> IsInUseAsync(Guid tenantId, Guid projectId, Guid statusId, string statusKey, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task RemoveAsync(WorkItemStatusDefinition definition, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
