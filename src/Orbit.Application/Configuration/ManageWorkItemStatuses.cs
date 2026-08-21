using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Orbit.Application.Abstractions;
using Orbit.Application.Boards;
using Orbit.Application.Caching;
using Orbit.Application.Common;
using Orbit.Application.Settings;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Choices;
using Orbit.Domain.Configuration;

namespace Orbit.Application.Configuration;

/// <summary>
/// The "Edit workflow" / "Add status" backend (ORBIT-WORK-MANAGEMENT-ARCHITECTURE.md §13.5
/// next-increment): CRUD over a project's <see cref="WorkItemStatusDefinition"/> catalog, which
/// replaced the previously fixed <c>WorkItemStatus</c> enum as the source of truth for
/// <c>WorkItem.StatusId</c> and <c>BoardColumn.StatusId</c>.
/// </summary>
public sealed record WorkItemStatusDefinitionDto(
    Guid Id,
    string Key,
    string Name,
    StatusCategory Category,
    int Order,
    string ColorToken,
    bool IsSystem,
    bool IsDefault,
    long Version)
{
    public static WorkItemStatusDefinitionDto From(WorkItemStatusDefinition definition) =>
        new(
            definition.Id,
            definition.Key,
            definition.Name,
            definition.Category,
            definition.Order,
            definition.ColorToken,
            definition.IsSystem,
            definition.IsDefault,
            definition.Version);
}

public sealed record ListWorkItemStatusesQuery(Guid ProjectId) : IQuery<IReadOnlyList<WorkItemStatusDefinitionDto>>;

public sealed class ListWorkItemStatusesValidator : AbstractValidator<ListWorkItemStatusesQuery>
{
    public ListWorkItemStatusesValidator() => RuleFor(query => query.ProjectId).NotEmpty();
}

/// <summary>
/// Cached per OBSERVABILITY-CACHING-ARCHITECTURE.md §5.2 row 2: the status catalog is read on
/// every item render and changes rarely. Keyed on Project.ConfigEpoch (read fresh from PostgreSQL
/// every call, per §5.1 principle 7) so a write in the handlers above is visible on the very next
/// read without an explicit cache-delete.
/// </summary>
public sealed class ListWorkItemStatusesHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    IWorkItemStatusRepository repository,
    HybridCache cache,
    ILogger<ListWorkItemStatusesHandler> logger)
    : IRequestHandler<ListWorkItemStatusesQuery, IReadOnlyList<WorkItemStatusDefinitionDto>>
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(5),
    };

    public async Task<IReadOnlyList<WorkItemStatusDefinitionDto>> Handle(
        ListWorkItemStatusesQuery request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(tenant.TenantId, request.ProjectId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");

        var key = TenantCacheKey.For(
            tenant.TenantId, "config", "statuses", $"{request.ProjectId}:v{project.ConfigEpoch}");
        return await CacheFailOpen.GetOrCreateAsync<IReadOnlyList<WorkItemStatusDefinitionDto>>(
            cache,
            logger,
            key,
            async token => (await repository.ListByProjectAsync(tenant.TenantId, request.ProjectId, token))
                .Select(WorkItemStatusDefinitionDto.From)
                .ToArray(),
            CacheOptions,
            cancellationToken);
    }
}

public sealed record CreateWorkItemStatusCommand(
    Guid ProjectId,
    string Key,
    string Name,
    StatusCategory Category,
    int Order,
    string ColorToken) : ICommand<WorkItemStatusDefinitionDto>;

public sealed class CreateWorkItemStatusValidator : AbstractValidator<CreateWorkItemStatusCommand>
{
    public CreateWorkItemStatusValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Key).NotEmpty().Length(1, 64).Matches("^[a-z0-9-]+$");
        RuleFor(command => command.Name).NotEmpty().Length(1, 60);
        RuleFor(command => command.Category).IsInEnum();
        RuleFor(command => command.Order).InclusiveBetween(0, 100_000);
        RuleFor(command => command.ColorToken).NotEmpty().MaximumLength(32).Matches("^[a-zA-Z0-9-]+$");
    }
}

/// <summary>
/// Creating a status also appends it as a board column when the project already has a board, so a
/// newly added status is immediately usable as a place to move cards rather than a silent addition
/// to the catalog that a user must separately discover in "Edit board" before it does anything.
/// </summary>
public sealed class CreateWorkItemStatusHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    IWorkItemStatusRepository repository,
    IBoardRepository boards,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<CreateWorkItemStatusCommand, WorkItemStatusDefinitionDto>
{
    public async Task<WorkItemStatusDefinitionDto> Handle(
        CreateWorkItemStatusCommand request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(tenant.TenantId, request.ProjectId, ProjectPermission.Administer, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");
        project.IncrementConfigEpoch();

        var existing = await repository.ListByProjectAsync(tenant.TenantId, request.ProjectId, cancellationToken);
        var normalizedKey = WorkItemStatusDefinition.NormalizeKey(request.Key);
        if (existing.Any(status => status.Key == normalizedKey))
        {
            throw new ValidationException("A status with this key already exists in this project.");
        }

        var now = timeProvider.GetUtcNow();
        var definition = WorkItemStatusDefinition.Create(
            tenant.TenantId, request.ProjectId, request.Key, request.Name, request.Category, request.Order,
            request.ColorToken, now);
        await repository.AddAsync(definition, cancellationToken);

        var board = await boards.GetAsync(tenant.TenantId, request.ProjectId, cancellationToken);
        if (board is not null)
        {
            var columns = board.Columns
                .Select(column => new BoardColumnInput(column.StatusId, column.WipLimit, column.WipLimitMode))
                .Append(new BoardColumnInput(definition.Id, null, WipLimitMode.Warn))
                .ToList();
            board.Update(board.Name, board.Type, columns, now);
            board.IncrementEpoch();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkItemStatusDefinitionDto.From(definition);
    }
}

public sealed record UpdateWorkItemStatusCommand(
    Guid ProjectId,
    Guid StatusId,
    string Name,
    StatusCategory Category,
    int Order,
    string ColorToken,
    long ExpectedVersion) : ICommand<WorkItemStatusDefinitionDto>;

public sealed class UpdateWorkItemStatusValidator : AbstractValidator<UpdateWorkItemStatusCommand>
{
    public UpdateWorkItemStatusValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.StatusId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().Length(1, 60);
        RuleFor(command => command.Category).IsInEnum();
        RuleFor(command => command.Order).InclusiveBetween(0, 100_000);
        RuleFor(command => command.ColorToken).NotEmpty().MaximumLength(32).Matches("^[a-zA-Z0-9-]+$");
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class UpdateWorkItemStatusHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    IWorkItemStatusRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateWorkItemStatusCommand, WorkItemStatusDefinitionDto>
{
    public async Task<WorkItemStatusDefinitionDto> Handle(
        UpdateWorkItemStatusCommand request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(tenant.TenantId, request.ProjectId, ProjectPermission.Administer, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");
        project.IncrementConfigEpoch();
        var definition = await repository.GetAsync(tenant.TenantId, request.ProjectId, request.StatusId, cancellationToken)
            ?? throw new NotFoundException("Status was not found.");
        SettingsConcurrency.EnsureVersion(
            true, definition.Version, request.ExpectedVersion, "The status changed after it was loaded.");

        // Recategorizing a status already referenced by history would silently rewrite how closed
        // sprints' cumulative-flow/cycle-time reports interpret every past transition through it
        // (they resolve a history entry's status key against the *current* catalog) - so once a
        // status has real history, only its presentation (name/order/color) may still change.
        if (request.Category != definition.Category
            && await repository.IsInUseAsync(
                tenant.TenantId, request.ProjectId, definition.Id, definition.Key, cancellationToken))
        {
            throw new ValidationException(
                "This status has work item history and its category can no longer be changed, " +
                "since that would silently reinterpret past agile reports. Create a new status instead.");
        }

        definition.Update(request.Name, request.Category, request.Order, request.ColorToken, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkItemStatusDefinitionDto.From(definition);
    }
}

public sealed record SetDefaultWorkItemStatusCommand(Guid ProjectId, Guid StatusId) : ICommand<WorkItemStatusDefinitionDto>;

public sealed class SetDefaultWorkItemStatusValidator : AbstractValidator<SetDefaultWorkItemStatusCommand>
{
    public SetDefaultWorkItemStatusValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.StatusId).NotEmpty();
    }
}

/// <summary>
/// Flips which status a newly created work item starts in - explicit and independent of display
/// `Order`, so reordering the workflow (§13.5 "Edit workflow") never silently changes it (unlike
/// the previous lowest-`Order`-wins behaviour).
/// </summary>
public sealed class SetDefaultWorkItemStatusHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    IWorkItemStatusRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<SetDefaultWorkItemStatusCommand, WorkItemStatusDefinitionDto>
{
    public async Task<WorkItemStatusDefinitionDto> Handle(
        SetDefaultWorkItemStatusCommand request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(tenant.TenantId, request.ProjectId, ProjectPermission.Administer, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");
        project.IncrementConfigEpoch();
        var target = await repository.GetAsync(tenant.TenantId, request.ProjectId, request.StatusId, cancellationToken)
            ?? throw new NotFoundException("Status was not found.");

        var now = timeProvider.GetUtcNow();
        var current = await repository.GetDefaultAsync(tenant.TenantId, request.ProjectId, cancellationToken);
        if (current is not null && current.Id != target.Id)
        {
            current.SetDefault(false, now);
        }

        target.SetDefault(true, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkItemStatusDefinitionDto.From(target);
    }
}

public sealed record DeleteWorkItemStatusCommand(Guid ProjectId, Guid StatusId) : ICommand<Unit>;

public sealed class DeleteWorkItemStatusValidator : AbstractValidator<DeleteWorkItemStatusCommand>
{
    public DeleteWorkItemStatusValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.StatusId).NotEmpty();
    }
}

public sealed class DeleteWorkItemStatusHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    IWorkItemStatusRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteWorkItemStatusCommand, Unit>
{
    public async Task<Unit> Handle(DeleteWorkItemStatusCommand request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(tenant.TenantId, request.ProjectId, ProjectPermission.Administer, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");
        project.IncrementConfigEpoch();
        var definition = await repository.GetAsync(tenant.TenantId, request.ProjectId, request.StatusId, cancellationToken)
            ?? throw new NotFoundException("Status was not found.");

        var remaining = await repository.ListByProjectAsync(tenant.TenantId, request.ProjectId, cancellationToken);
        if (remaining.Count <= 1)
        {
            throw new ValidationException("A project's workflow needs at least one status.");
        }

        if (definition.IsDefault)
        {
            throw new ValidationException(
                "This is the default status for new work items and cannot be deleted. Set another status as default first.");
        }

        if (await repository.IsInUseAsync(
                tenant.TenantId, request.ProjectId, request.StatusId, definition.Key, cancellationToken))
        {
            throw new ValidationException(
                "This status is in use by a work item, a board column, or work item history and cannot be deleted.");
        }

        await repository.RemoveAsync(definition, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
