using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

public enum WorkItemLinkDirection
{
    Outgoing,
    Incoming
}

/// <summary>
/// A work item link as seen from the perspective of the work item it was fetched for: the fields
/// describe the *other* item, and <see cref="Direction"/> says whether the requested item is the
/// link's source (Outgoing, e.g. "Blocks X") or target (Incoming, e.g. "Is blocked by X").
/// </summary>
public sealed record WorkItemLinkDto(
    Guid Id,
    WorkItemLinkKind Kind,
    WorkItemLinkDirection Direction,
    Guid WorkItemId,
    string Key,
    string Summary,
    WorkItemType Type,
    WorkItemStatus Status)
{
    public static WorkItemLinkDto From(WorkItemLink link, WorkItem requestedItem, WorkItem other) =>
        new(
            link.Id,
            link.Kind,
            link.SourceWorkItemId == requestedItem.Id ? WorkItemLinkDirection.Outgoing : WorkItemLinkDirection.Incoming,
            other.Id,
            other.Key,
            other.Summary,
            other.Type,
            other.Status);
}

// ---------------------------------------------------------------------------
// List links
// ---------------------------------------------------------------------------

public sealed record ListWorkItemLinksQuery(Guid WorkItemId) : IQuery<IReadOnlyList<WorkItemLinkDto>>;

public sealed class ListWorkItemLinksValidator : AbstractValidator<ListWorkItemLinksQuery>
{
    public ListWorkItemLinksValidator() => RuleFor(query => query.WorkItemId).NotEmpty();
}

public sealed class ListWorkItemLinksHandler(
    ITenantContext tenantContext,
    IWorkItemRepository workItems,
    IWorkItemLinkRepository links) : IRequestHandler<ListWorkItemLinksQuery, IReadOnlyList<WorkItemLinkDto>>
{
    public async Task<IReadOnlyList<WorkItemLinkDto>> Handle(
        ListWorkItemLinksQuery request, CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetAsync(
                tenantContext.TenantId, request.WorkItemId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        var itemLinks = await links.ListByWorkItemAsync(tenantContext.TenantId, workItem.Id, cancellationToken);
        if (itemLinks.Count == 0)
        {
            return [];
        }

        var otherIds = itemLinks
            .Select(link => link.SourceWorkItemId == workItem.Id ? link.TargetWorkItemId : link.SourceWorkItemId)
            .Distinct()
            .ToArray();
        var others = await workItems.ListByIdsAsync(
            tenantContext.TenantId, otherIds, ProjectPermission.View, cancellationToken);
        var othersById = others.ToDictionary(item => item.Id);

        return itemLinks
            .Select(link =>
            {
                var otherId = link.SourceWorkItemId == workItem.Id ? link.TargetWorkItemId : link.SourceWorkItemId;
                return othersById.TryGetValue(otherId, out var other) ? WorkItemLinkDto.From(link, workItem, other) : null;
            })
            .Where(dto => dto is not null)
            .Select(dto => dto!)
            .ToArray();
    }
}

// ---------------------------------------------------------------------------
// Add link
// ---------------------------------------------------------------------------

/// <summary>
/// <paramref name="Inverse"/> is true when the caller picked an inverse relationship label (e.g.
/// "Is blocked by") in the UI, meaning <paramref name="WorkItemId"/> is the link's target rather
/// than its source.
/// </summary>
public sealed record AddWorkItemLinkCommand(
    Guid WorkItemId,
    WorkItemLinkKind Kind,
    Guid TargetWorkItemId,
    bool Inverse) : ICommand<WorkItemLinkDto>;

public sealed class AddWorkItemLinkValidator : AbstractValidator<AddWorkItemLinkCommand>
{
    public AddWorkItemLinkValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.TargetWorkItemId).NotEmpty();
        RuleFor(command => command.Kind).IsInEnum();
        RuleFor(command => command)
            .Must(command => command.WorkItemId != command.TargetWorkItemId)
            .WithMessage("A work item cannot link to itself.");
    }
}

public sealed class AddWorkItemLinkHandler(
    ITenantContext tenantContext,
    IWorkItemRepository workItems,
    IWorkItemLinkRepository links,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<AddWorkItemLinkCommand, WorkItemLinkDto>
{
    public async Task<WorkItemLinkDto> Handle(AddWorkItemLinkCommand request, CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetAsync(
                tenantContext.TenantId, request.WorkItemId, ProjectPermission.TransitionWorkItem, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");
        var target = await workItems.GetAsync(
                tenantContext.TenantId, request.TargetWorkItemId, ProjectPermission.View, cancellationToken)
            ?? throw new ValidationException("The selected work item was not found.");

        var (sourceId, targetId) = request.Inverse
            ? (target.Id, workItem.Id)
            : (workItem.Id, target.Id);

        if (await links.ExistsAsync(tenantContext.TenantId, sourceId, targetId, request.Kind, cancellationToken))
        {
            throw new ValidationException("This relationship already exists.");
        }

        var link = WorkItemLink.Create(tenantContext.TenantId, sourceId, targetId, request.Kind, timeProvider.GetUtcNow());
        await links.AddAsync(link, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return WorkItemLinkDto.From(link, workItem, target);
    }
}

// ---------------------------------------------------------------------------
// Remove link
// ---------------------------------------------------------------------------

public sealed record RemoveWorkItemLinkCommand(Guid WorkItemId, Guid LinkId) : ICommand<Unit>;

public sealed class RemoveWorkItemLinkValidator : AbstractValidator<RemoveWorkItemLinkCommand>
{
    public RemoveWorkItemLinkValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.LinkId).NotEmpty();
    }
}

public sealed class RemoveWorkItemLinkHandler(
    ITenantContext tenantContext,
    IWorkItemRepository workItems,
    IWorkItemLinkRepository links,
    IUnitOfWork unitOfWork) : IRequestHandler<RemoveWorkItemLinkCommand, Unit>
{
    public async Task<Unit> Handle(RemoveWorkItemLinkCommand request, CancellationToken cancellationToken)
    {
        _ = await workItems.GetAsync(
                tenantContext.TenantId, request.WorkItemId, ProjectPermission.TransitionWorkItem, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        var link = await links.GetAsync(tenantContext.TenantId, request.LinkId, cancellationToken)
            ?? throw new NotFoundException("Link was not found.");
        if (link.SourceWorkItemId != request.WorkItemId && link.TargetWorkItemId != request.WorkItemId)
        {
            throw new NotFoundException("Link was not found.");
        }

        await links.RemoveAsync(link, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
