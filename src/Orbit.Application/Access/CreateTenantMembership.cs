using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Identity;

namespace Orbit.Application.Access;

public sealed record CreateTenantMembershipCommand(
    string Issuer,
    string Subject,
    PrincipalType PrincipalType,
    TenantRole Role) : ICommand<TenantMembershipDto>;

public sealed record TenantMembershipDto(
    Guid Id,
    Guid? UserId,
    string? Issuer,
    string? Subject,
    PrincipalType PrincipalType,
    TenantRole Role,
    MembershipTier Tier,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string? DisplayName,
    string? AvatarUrl)
{
    public static TenantMembershipDto From(TenantMembership membership, UserAccount? account = null) =>
        new(
            membership.Id,
            membership.UserId,
            membership.Issuer,
            membership.Subject,
            membership.PrincipalType,
            membership.Role,
            membership.Tier,
            membership.IsActive,
            membership.CreatedAt,
            account?.DisplayName,
            account?.AvatarUrl);
}

public sealed class CreateTenantMembershipValidator : AbstractValidator<CreateTenantMembershipCommand>
{
    public CreateTenantMembershipValidator()
    {
        RuleFor(command => command.Issuer).NotEmpty().MaximumLength(512);
        RuleFor(command => command.Subject).NotEmpty().MaximumLength(255);
        RuleFor(command => command.PrincipalType).IsInEnum();
        RuleFor(command => command.Role).IsInEnum();
    }
}

public sealed class CreateTenantMembershipHandler(
    ITenantContext tenantContext,
    ITenantAuthorization authorization,
    ITenantMembershipRepository memberships,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<CreateTenantMembershipCommand, TenantMembershipDto>
{
    public async Task<TenantMembershipDto> Handle(
        CreateTenantMembershipCommand request,
        CancellationToken cancellationToken)
    {
        if (!authorization.CanCreateMembership(request.Role))
        {
            throw new AccessDeniedException("The current principal cannot grant this tenant role.");
        }

        var membership = TenantMembership.Create(
            tenantContext.TenantId,
            request.Issuer,
            request.Subject,
            request.PrincipalType,
            request.Role,
            timeProvider.GetUtcNow());
        await memberships.AddAsync(membership, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TenantMembershipDto.From(membership);
    }
}
