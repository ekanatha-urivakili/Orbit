using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;

namespace Orbit.Application.Access;

public sealed record ChangeTenantMembershipRoleCommand(Guid MembershipId, TenantRole Role) : ICommand<TenantMembershipDto>;

public sealed class ChangeTenantMembershipRoleValidator : AbstractValidator<ChangeTenantMembershipRoleCommand>
{
    public ChangeTenantMembershipRoleValidator()
    {
        RuleFor(command => command.MembershipId).NotEmpty();
        RuleFor(command => command.Role).IsInEnum();
    }
}

public sealed class ChangeTenantMembershipRoleHandler(
    ITenantContext tenantContext,
    ITenantAuthorization authorization,
    ITenantOwnerLock ownerLock,
    ITenantMembershipRepository memberships,
    ISettingsRepository settings,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ChangeTenantMembershipRoleCommand, TenantMembershipDto>
{
    public async Task<TenantMembershipDto> Handle(
        ChangeTenantMembershipRoleCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        await ownerLock.AcquireAsync(tenantId, cancellationToken);
        var membership = await memberships.GetActiveAsync(tenantId, request.MembershipId, cancellationToken)
            ?? throw new NotFoundException("Workspace membership was not found.");

        if (!authorization.CanCreateMembership(request.Role) || !authorization.CanCreateMembership(membership.Role))
        {
            throw new AccessDeniedException("The current principal cannot grant this tenant role.");
        }

        if (membership.Role == TenantRole.Owner
            && request.Role != TenantRole.Owner
            && await IsSoleOwnerAsync(tenantId, membership.Id, cancellationToken))
        {
            throw new ConflictException("The workspace must retain at least one owner.");
        }

        membership.ChangeRole(request.Role);
        var workspace = await settings.GetWorkspaceAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException("Workspace was not found.");
        workspace.IncrementAuthorizationEpoch();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TenantMembershipDto.From(membership);
    }

    private async Task<bool> IsSoleOwnerAsync(Guid tenantId, Guid membershipId, CancellationToken cancellationToken)
    {
        var all = await memberships.ListAsync(tenantId, cancellationToken);
        return !all.Any(other =>
            other.Id != membershipId && other.IsActive && other.Role == TenantRole.Owner);
    }
}

public sealed record DeactivateTenantMembershipCommand(Guid MembershipId) : ICommand<Unit>;

public sealed class DeactivateTenantMembershipHandler(
    ITenantContext tenantContext,
    ITenantAuthorization authorization,
    ITenantOwnerLock ownerLock,
    ITenantMembershipRepository memberships,
    ISettingsRepository settings,
    IUnitOfWork unitOfWork) : IRequestHandler<DeactivateTenantMembershipCommand, Unit>
{
    public async Task<Unit> Handle(DeactivateTenantMembershipCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        await ownerLock.AcquireAsync(tenantId, cancellationToken);
        var membership = await memberships.GetActiveAsync(tenantId, request.MembershipId, cancellationToken)
            ?? throw new NotFoundException("Workspace membership was not found.");

        if (!authorization.CanCreateMembership(membership.Role))
        {
            throw new AccessDeniedException("The current principal cannot remove this tenant member.");
        }

        if (membership.Role == TenantRole.Owner && await IsSoleOwnerAsync(tenantId, membership.Id, cancellationToken))
        {
            throw new ConflictException("The workspace must retain at least one owner.");
        }

        membership.Deactivate();
        var workspace = await settings.GetWorkspaceAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException("Workspace was not found.");
        workspace.IncrementAuthorizationEpoch();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    private async Task<bool> IsSoleOwnerAsync(Guid tenantId, Guid membershipId, CancellationToken cancellationToken)
    {
        var all = await memberships.ListAsync(tenantId, cancellationToken);
        return !all.Any(other =>
            other.Id != membershipId && other.IsActive && other.Role == TenantRole.Owner);
    }
}
