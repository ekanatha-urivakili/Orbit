using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;

namespace Orbit.Application.Projects;

public sealed record ListProjectsQuery(int Skip = 0, int Take = Paging.DefaultTake) : IQuery<PagedResult<ProjectDto>>;

public sealed class ListProjectsValidator : AbstractValidator<ListProjectsQuery>
{
    public ListProjectsValidator()
    {
        RuleFor(query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(query => query.Take).InclusiveBetween(1, Paging.MaxTake);
    }
}

public sealed class ListProjectsHandler(ITenantContext tenantContext, IProjectRepository projects)
    : IRequestHandler<ListProjectsQuery, PagedResult<ProjectDto>>
{
    public async Task<PagedResult<ProjectDto>> Handle(
        ListProjectsQuery request,
        CancellationToken cancellationToken)
    {
        var result = await projects.ListAsync(
            tenantContext.TenantId,
            ProjectPermission.View,
            request.Skip,
            request.Take,
            cancellationToken);
        return new PagedResult<ProjectDto>(result.Items.Select(ProjectDto.From).ToArray(), result.TotalCount);
    }
}
