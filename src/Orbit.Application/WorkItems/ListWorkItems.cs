using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;

namespace Orbit.Application.WorkItems;

public sealed record ListWorkItemsQuery(
    Guid ProjectId,
    int Skip = 0,
    int Take = Paging.DefaultTake) : IQuery<PagedResult<WorkItemDto>>;

public sealed class ListWorkItemsValidator : AbstractValidator<ListWorkItemsQuery>
{
    public ListWorkItemsValidator()
    {
        RuleFor(query => query.ProjectId).NotEmpty();
        RuleFor(query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(query => query.Take).InclusiveBetween(1, Paging.MaxTake);
    }
}

public sealed class ListWorkItemsHandler(ITenantContext tenantContext, IWorkItemRepository workItems)
    : IRequestHandler<ListWorkItemsQuery, PagedResult<WorkItemDto>>
{
    public async Task<PagedResult<WorkItemDto>> Handle(
        ListWorkItemsQuery request,
        CancellationToken cancellationToken)
    {
        var result = await workItems.ListByProjectAsync(
            tenantContext.TenantId,
            request.ProjectId,
            ProjectPermission.View,
            request.Skip,
            request.Take,
            cancellationToken);
        return new PagedResult<WorkItemDto>(result.Items.Select(WorkItemDto.From).ToArray(), result.TotalCount);
    }
}
