using Orbit.Domain.Choices;
using Orbit.Domain.Common;

namespace Orbit.Domain.Boards;

public sealed record BoardColumnInput(Guid StatusId, int? WipLimit, WipLimitMode WipLimitMode);

public sealed class BoardColumn
{
    private BoardColumn()
    {
    }

    internal BoardColumn(Guid statusId, int order, int? wipLimit, WipLimitMode wipLimitMode)
    {
        StatusId = statusId;
        Order = order;
        WipLimit = wipLimit;
        WipLimitMode = wipLimitMode;
    }

    public Guid StatusId { get; private set; }
    public int Order { get; private set; }
    public int? WipLimit { get; private set; }
    public WipLimitMode WipLimitMode { get; private set; }
}

public sealed class Board
{
    private readonly List<BoardColumn> _columns = [];

    private Board()
    {
    }

    private Board(Guid tenantId, Guid projectId, string name, BoardType type, DateTimeOffset now)
    {
        TenantId = tenantId;
        ProjectId = projectId;
        Name = name;
        Type = type;
        Version = 1;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public BoardType Type { get; private set; }
    public long Version { get; private set; }

    /// <summary>
    /// Bumped on any write affecting the board (column/config update, item transition, rank
    /// change - OBSERVABILITY-CACHING-ARCHITECTURE.md §5.2 row 1), separate from Version's
    /// optimistic-concurrency role. Mirrors Workspace.AuthorizationEpoch/Project.ConfigEpoch.
    /// </summary>
    public long Epoch { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<BoardColumn> Columns => _columns;

    public static Board Create(
        Guid tenantId,
        Guid projectId,
        string name,
        BoardType type,
        IReadOnlyList<BoardColumnInput> columns,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty)
        {
            throw new DomainException("Tenant and project ids are required.");
        }

        var board = new Board(tenantId, projectId, NormalizeName(name), type, now);
        board.ReplaceColumns(columns);
        return board;
    }

    public void Update(string name, BoardType type, IReadOnlyList<BoardColumnInput> columns, DateTimeOffset now)
    {
        Name = NormalizeName(name);
        Type = type;
        ReplaceColumns(columns);
        Version++;
        UpdatedAt = now;
    }

    public void IncrementEpoch() => Epoch++;

    private void ReplaceColumns(IReadOnlyList<BoardColumnInput> columns)
    {
        if (columns.Count == 0)
        {
            throw new DomainException("A board needs at least one column.");
        }

        if (columns.Select(column => column.StatusId).Distinct().Count() != columns.Count)
        {
            throw new DomainException("Board columns must reference distinct statuses.");
        }

        if (columns.Any(column => column.WipLimit is < 1))
        {
            throw new DomainException("A column WIP limit must be at least 1 when set.");
        }

        _columns.Clear();
        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            _columns.Add(new BoardColumn(column.StatusId, index, column.WipLimit, column.WipLimitMode));
        }
    }

    private static string NormalizeName(string name)
    {
        var normalized = name.Trim();
        if (normalized.Length is < 2 or > 120)
        {
            throw new DomainException("Board name must contain 2 to 120 characters.");
        }

        return normalized;
    }
}
