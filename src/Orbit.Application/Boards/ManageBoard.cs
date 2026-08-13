using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.Settings;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Choices;

namespace Orbit.Application.Boards;

public sealed record BoardColumnDto(WorkItemStatus Status, int Order, int? WipLimit, WipLimitMode WipLimitMode)
{
    public static BoardColumnDto From(BoardColumn column) =>
        new(column.Status, column.Order, column.WipLimit, column.WipLimitMode);
}

public sealed record BoardDto(Guid ProjectId, string Name, BoardType Type, long Version, IReadOnlyList<BoardColumnDto> Columns)
{
    public static BoardDto From(Board board) =>
        new(
            board.ProjectId,
            board.Name,
            board.Type,
            board.Version,
            board.Columns.Count > 0 ? [.. board.Columns.Select(BoardColumnDto.From)] : DefaultColumns);

    public static IReadOnlyList<BoardColumnDto> DefaultColumns { get; } =
    [
        .. SystemChoiceCatalog.WorkItemStatuses
            .OrderBy(status => status.Order)
            .Select((status, index) => new BoardColumnDto(status.Value, index, null, WipLimitMode.Warn)),
    ];
}

public sealed record GetBoardQuery(Guid ProjectId) : IQuery<BoardDto>;

public sealed class GetBoardHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    IBoardRepository boards) : IRequestHandler<GetBoardQuery, BoardDto>
{
    public async Task<BoardDto> Handle(GetBoardQuery request, CancellationToken cancellationToken)
    {
        _ = await projects.GetAsync(tenant.TenantId, request.ProjectId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");
        var board = await boards.GetAsync(tenant.TenantId, request.ProjectId, cancellationToken);
        return board is null
            ? new BoardDto(request.ProjectId, string.Empty, BoardType.Kanban, 0, BoardDto.DefaultColumns)
            : BoardDto.From(board);
    }
}

public sealed record UpdateBoardColumnInput(WorkItemStatus Status, int? WipLimit, WipLimitMode WipLimitMode);

public sealed record UpdateBoardCommand(
    Guid ProjectId,
    string Name,
    BoardType Type,
    IReadOnlyList<UpdateBoardColumnInput> Columns,
    long ExpectedVersion) : ICommand<BoardDto>;

public sealed class UpdateBoardValidator : AbstractValidator<UpdateBoardCommand>
{
    public UpdateBoardValidator()
    {
        RuleFor(command => command.Name).NotEmpty().Length(2, 120);
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleForEach(command => command.Columns).ChildRules(column =>
        {
            column.RuleFor(c => c.Status).IsInEnum();
            column.RuleFor(c => c.WipLimitMode).IsInEnum();
            column.RuleFor(c => c.WipLimit).GreaterThanOrEqualTo(1).When(c => c.WipLimit.HasValue);
        });
    }
}

public sealed class UpdateBoardHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    IBoardRepository boards,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<UpdateBoardCommand, BoardDto>
{
    public async Task<BoardDto> Handle(UpdateBoardCommand request, CancellationToken cancellationToken)
    {
        _ = await projects.GetAsync(tenant.TenantId, request.ProjectId, ProjectPermission.Administer, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");
        var board = await boards.GetAsync(tenant.TenantId, request.ProjectId, cancellationToken);
        SettingsConcurrency.EnsureVersion(
            board is not null,
            board?.Version ?? 0,
            request.ExpectedVersion,
            "The board changed after it was loaded.");

        var columns = request.Columns.Count > 0
            ? request.Columns.Select(c => new BoardColumnInput(c.Status, c.WipLimit, c.WipLimitMode)).ToList()
            : board is not null
                ? board.Columns.Select(c => new BoardColumnInput(c.Status, c.WipLimit, c.WipLimitMode)).ToList()
                : BoardDto.DefaultColumns.Select(c => new BoardColumnInput(c.Status, c.WipLimit, c.WipLimitMode)).ToList();

        if (board is null)
        {
            board = Board.Create(
                tenant.TenantId, request.ProjectId, request.Name, request.Type, columns, timeProvider.GetUtcNow());
            await boards.AddAsync(board, cancellationToken);
        }
        else
        {
            board.Update(request.Name, request.Type, columns, timeProvider.GetUtcNow());
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return BoardDto.From(board);
    }
}
