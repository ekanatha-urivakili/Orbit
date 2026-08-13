using MediatR;
using Orbit.Application.Abstractions;

namespace Orbit.Application.Access;

public sealed record ListTenantMembershipsQuery : IQuery<IReadOnlyList<TenantMembershipDto>>;

public sealed class ListTenantMembershipsHandler(
    ITenantContext tenantContext,
    ITenantMembershipRepository memberships)
    : IRequestHandler<ListTenantMembershipsQuery, IReadOnlyList<TenantMembershipDto>>
{
    public async Task<IReadOnlyList<TenantMembershipDto>> Handle(
        ListTenantMembershipsQuery request,
        CancellationToken cancellationToken)
    {
        var result = await memberships.ListAsync(tenantContext.TenantId, cancellationToken);
        return result.Select(TenantMembershipDto.From).ToArray();
    }
}
