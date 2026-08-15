using MediatR;
using Orbit.Application.Abstractions;

namespace Orbit.Application.Access;

public sealed record ListTenantMembershipsQuery : IQuery<IReadOnlyList<TenantMembershipDto>>;

public sealed class ListTenantMembershipsHandler(
    ITenantContext tenantContext,
    ITenantMembershipRepository memberships,
    ISettingsRepository settings)
    : IRequestHandler<ListTenantMembershipsQuery, IReadOnlyList<TenantMembershipDto>>
{
    public async Task<IReadOnlyList<TenantMembershipDto>> Handle(
        ListTenantMembershipsQuery request,
        CancellationToken cancellationToken)
    {
        var result = await memberships.ListAsync(tenantContext.TenantId, cancellationToken);
        var userIds = result
            .Where(membership => membership.UserId.HasValue)
            .Select(membership => membership.UserId!.Value)
            .Distinct()
            .ToArray();
        var accounts = await settings.GetUserAccountsAsync(userIds, cancellationToken);
        var accountsById = accounts.ToDictionary(account => account.Id);

        return result
            .Select(membership => TenantMembershipDto.From(
                membership,
                membership.UserId.HasValue && accountsById.TryGetValue(membership.UserId.Value, out var account)
                    ? account
                    : null))
            .ToArray();
    }
}
