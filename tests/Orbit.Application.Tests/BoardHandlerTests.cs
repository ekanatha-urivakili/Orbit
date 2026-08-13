using Orbit.Application.Abstractions;
using Orbit.Application.Boards;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Choices;
using Orbit.Domain.Projects;

namespace Orbit.Application.Tests;

public sealed class BoardHandlerTests
{
    [Fact]
    public async Task GetBoard_ReturnsZeroVersionSentinel_WhenNoBoardExists()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var handler = new GetBoardHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.View]),
            new BoardRepositoryStub());

        var result = await handler.Handle(new GetBoardQuery(project.Id), CancellationToken.None);

        Assert.Equal(0, result.Version);
    }

    [Fact]
    public async Task UpdateBoard_CreatesBoard_OnFirstUpdate()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var boards = new BoardRepositoryStub();
        var handler = new UpdateBoardHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            boards,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new UpdateBoardCommand(project.Id, "Delivery Board", BoardType.Kanban, [], 0),
            CancellationToken.None);

        Assert.Equal("Delivery Board", result.Name);
        Assert.Equal(1, result.Version);
        Assert.NotNull(boards.Added);
        Assert.Equal(tenantId, boards.Added!.TenantId);
        Assert.Equal(BoardDto.DefaultColumns.Count, result.Columns.Count);
    }

    [Fact]
    public async Task UpdateBoard_AppliesRequestedColumnsAndWipLimits()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var boards = new BoardRepositoryStub();
        var handler = new UpdateBoardHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            boards,
            new UnitOfWorkStub(),
            TimeProvider.System);

        IReadOnlyList<UpdateBoardColumnInput> requestedColumns =
        [
            new(WorkItemStatus.Backlog, null, WipLimitMode.Warn),
            new(WorkItemStatus.InProgress, 3, WipLimitMode.Block),
        ];

        var result = await handler.Handle(
            new UpdateBoardCommand(project.Id, "Delivery Board", BoardType.Kanban, requestedColumns, 0),
            CancellationToken.None);

        Assert.Equal(2, result.Columns.Count);
        Assert.Equal(WorkItemStatus.InProgress, result.Columns[1].Status);
        Assert.Equal(3, result.Columns[1].WipLimit);
        Assert.Equal(WipLimitMode.Block, result.Columns[1].WipLimitMode);
    }

    [Fact]
    public async Task UpdateBoard_PreservesExistingColumns_WhenNoneRequested()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        IReadOnlyList<BoardColumnInput> initialColumns = [new(WorkItemStatus.Done, 2, WipLimitMode.Block)];
        var existing = Board.Create(tenantId, project.Id, "Delivery Board", BoardType.Kanban, initialColumns, DateTimeOffset.UtcNow);
        var handler = new UpdateBoardHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            new BoardRepositoryStub { Existing = existing },
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new UpdateBoardCommand(project.Id, "Renamed", BoardType.Scrum, [], 1),
            CancellationToken.None);

        Assert.Single(result.Columns);
        Assert.Equal(WorkItemStatus.Done, result.Columns[0].Status);
        Assert.Equal(2, result.Columns[0].WipLimit);
    }

    [Fact]
    public async Task UpdateBoard_RejectsStaleVersion()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        IReadOnlyList<BoardColumnInput> columns = [new(WorkItemStatus.Backlog, null, WipLimitMode.Warn)];
        var existing = Board.Create(tenantId, project.Id, "Delivery Board", BoardType.Kanban, columns, DateTimeOffset.UtcNow);
        var handler = new UpdateBoardHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            new BoardRepositoryStub { Existing = existing },
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
        var handler = new UpdateBoardHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.View]),
            new BoardRepositoryStub(),
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

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
