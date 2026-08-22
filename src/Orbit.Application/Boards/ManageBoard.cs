using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Orbit.Application.Abstractions;
using Orbit.Application.Caching;
using Orbit.Application.Common;
using Orbit.Application.Settings;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Choices;

namespace Orbit.Application.Boards;

public sealed record BoardColumnDto(Guid StatusId, int Order, int? WipLimit, WipLimitMode WipLimitMode)
{
    public static BoardColumnDto From(BoardColumn column) =>
        new(column.StatusId, column.Order, column.WipLimit, column.WipLimitMode);
}

public sealed record BoardDto(Guid ProjectId, string Name, BoardType Type, long Version, IReadOnlyList<BoardColumnDto> Columns)
{
    public static BoardDto From(Board board) => new(board.ProjectId, board.Name, board.Type, board.Version, [.. board.Columns.Select(BoardColumnDto.From)]);

    public static BoardDto CreateDefault(Guid projectId, IReadOnlyList<Orbit.Domain.Configuration.WorkItemStatusDefinition> statuses) =>
        new(
            projectId,
            string.Empty,
            BoardType.Kanban,
            0,
            [.. statuses
                .OrderBy(status => status.Order)
                .Select((status, index) => new BoardColumnDto(status.Id, index, null, WipLimitMode.Warn))]);
}

public sealed record GetBoardQuery(Guid ProjectId) : IQuery<BoardDto>;

/// <summary>
/// The board's own columns/config are cached per OBSERVABILITY-CACHING-ARCHITECTURE.md §5.2 row
/// 1, epoch-bumped on any write affecting the board. Only applies once a Board row exists - the
/// synthesized "no board yet" default has no epoch to key on and is cheap to recompute regardless.
/// </summary>
public sealed class GetBoardHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    IBoardRepository boards,
    IWorkItemStatusRepository workItemStatuses,
    HybridCache cache,
    ILogger<GetBoardHandler> logger) : IRequestHandler<GetBoardQuery, BoardDto>
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(60),
        LocalCacheExpiration = TimeSpan.FromSeconds(60),
    };

    public async Task<BoardDto> Handle(GetBoardQuery request, CancellationToken cancellationToken)
    {
        _ = await projects.GetAsync(tenant.TenantId, request.ProjectId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");
        var board = await boards.GetAsync(tenant.TenantId, request.ProjectId, cancellationToken);
        if (board is null)
        {
            var statuses = await workItemStatuses.ListByProjectAsync(tenant.TenantId, request.ProjectId, cancellationToken);
            return BoardDto.CreateDefault(request.ProjectId, statuses);
        }

        var key = TenantCacheKey.For(tenant.TenantId, "board", "config", $"{request.ProjectId}:v{board.Epoch}");
        return await CacheFailOpen.GetOrCreateAsync(
            cache,
            logger,
            key,
            token => ValueTask.FromResult(BoardDto.From(board)),
            CacheOptions,
            cancellationToken);
    }
}

public sealed record UpdateBoardColumnInput(Guid StatusId, int? WipLimit, WipLimitMode WipLimitMode);

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
            column.RuleFor(c => c.StatusId).NotEmpty();
            column.RuleFor(c => c.WipLimitMode).IsInEnum();
            column.RuleFor(c => c.WipLimit).GreaterThanOrEqualTo(1).When(c => c.WipLimit.HasValue);
        });
    }
}

public sealed class UpdateBoardHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    IBoardRepository boards,
    IWorkItemStatusRepository workItemStatuses,
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

        var projectStatuses = await workItemStatuses.ListByProjectAsync(tenant.TenantId, request.ProjectId, cancellationToken);
        var validStatusIds = projectStatuses.Select(status => status.Id).ToHashSet();

        var columns = request.Columns.Count > 0
            ? request.Columns.Select(c => new BoardColumnInput(c.StatusId, c.WipLimit, c.WipLimitMode)).ToList()
            : board is not null
                ? board.Columns.Select(c => new BoardColumnInput(c.StatusId, c.WipLimit, c.WipLimitMode)).ToList()
                : projectStatuses
                    .OrderBy(status => status.Order)
                    .Select(status => new BoardColumnInput(status.Id, null, WipLimitMode.Warn))
                    .ToList();

        if (columns.Any(column => !validStatusIds.Contains(column.StatusId)))
        {
            throw new ValidationException("A board column references a status outside this project's workflow.");
        }

        if (board is null)
        {
            board = Board.Create(
                tenant.TenantId, request.ProjectId, request.Name, request.Type, columns, timeProvider.GetUtcNow());
            await boards.AddAsync(board, cancellationToken);
        }
        else
        {
            board.Update(request.Name, request.Type, columns, timeProvider.GetUtcNow());
            board.IncrementEpoch();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return BoardDto.From(board);
    }
}
