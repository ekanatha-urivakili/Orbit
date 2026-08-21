using Orbit.Domain.Boards;
using Orbit.Domain.Choices;
using Orbit.Domain.Common;

namespace Orbit.Domain.Tests;

public sealed class BoardModelTests
{
    private static readonly Guid BacklogStatusId = Guid.NewGuid();
    private static readonly Guid InProgressStatusId = Guid.NewGuid();
    private static readonly Guid DoneStatusId = Guid.NewGuid();

    private static readonly IReadOnlyList<BoardColumnInput> DefaultColumns =
    [
        new(BacklogStatusId, null, WipLimitMode.Warn),
        new(InProgressStatusId, 3, WipLimitMode.Block),
    ];

    [Fact]
    public void Board_Create_TrimsName()
    {
        var board = Board.Create(
            Guid.NewGuid(), Guid.NewGuid(), "  Delivery Board  ", BoardType.Kanban, DefaultColumns, DateTimeOffset.UtcNow);

        Assert.Equal("Delivery Board", board.Name);
        Assert.Equal(BoardType.Kanban, board.Type);
        Assert.Equal(1, board.Version);
    }

    [Fact]
    public void Board_Create_RejectsTooShortName()
    {
        var action = () => Board.Create(
            Guid.NewGuid(), Guid.NewGuid(), "A", BoardType.Kanban, DefaultColumns, DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Board_Create_RejectsEmptyIdentifiers()
    {
        var action = () => Board.Create(
            Guid.Empty, Guid.NewGuid(), "Delivery Board", BoardType.Kanban, DefaultColumns, DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Board_Create_SeedsColumnsInOrderWithWipLimits()
    {
        var board = Board.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Delivery Board", BoardType.Kanban, DefaultColumns, DateTimeOffset.UtcNow);

        Assert.Equal(2, board.Columns.Count);
        Assert.Equal(BacklogStatusId, board.Columns[0].StatusId);
        Assert.Equal(0, board.Columns[0].Order);
        Assert.Null(board.Columns[0].WipLimit);
        Assert.Equal(InProgressStatusId, board.Columns[1].StatusId);
        Assert.Equal(1, board.Columns[1].Order);
        Assert.Equal(3, board.Columns[1].WipLimit);
        Assert.Equal(WipLimitMode.Block, board.Columns[1].WipLimitMode);
    }

    [Fact]
    public void Board_Create_RejectsEmptyColumns()
    {
        var action = () => Board.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Delivery Board", BoardType.Kanban, [], DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Board_Create_RejectsDuplicateColumnStatuses()
    {
        IReadOnlyList<BoardColumnInput> columns =
        [
            new(BacklogStatusId, null, WipLimitMode.Warn),
            new(BacklogStatusId, null, WipLimitMode.Warn),
        ];

        var action = () => Board.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Delivery Board", BoardType.Kanban, columns, DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Board_Create_RejectsNonPositiveWipLimit()
    {
        IReadOnlyList<BoardColumnInput> columns = [new(BacklogStatusId, 0, WipLimitMode.Warn)];

        var action = () => Board.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Delivery Board", BoardType.Kanban, columns, DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Board_Update_BumpsVersionAndTimestamp()
    {
        var now = DateTimeOffset.UtcNow;
        var board = Board.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Delivery Board", BoardType.Kanban, DefaultColumns, now);

        IReadOnlyList<BoardColumnInput> updatedColumns = [new(DoneStatusId, 5, WipLimitMode.Warn)];
        board.Update("Renamed Board", BoardType.Scrum, updatedColumns, now.AddMinutes(5));

        Assert.Equal("Renamed Board", board.Name);
        Assert.Equal(BoardType.Scrum, board.Type);
        Assert.Equal(2, board.Version);
        Assert.Equal(now.AddMinutes(5), board.UpdatedAt);
        Assert.Single(board.Columns);
        Assert.Equal(DoneStatusId, board.Columns[0].StatusId);
    }
}
