using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Identity;

namespace Orbit.Application.Identity;

public sealed record ExternalIdentityDto(Guid Id, string Issuer, string Subject, DateTimeOffset CreatedAt)
{
    public static ExternalIdentityDto From(ExternalIdentity identity) =>
        new(identity.Id, identity.Issuer, identity.Subject, identity.CreatedAt);
}

/// <summary>
/// Links an external OIDC identity to the caller's own account. Requires an already-authenticated
/// local session - there is no anonymous auto-provisioning path, so an unrecognized OIDC identity
/// never silently creates or attaches to an account without the user first proving who they are.
/// </summary>
public sealed record LinkExternalIdentityCommand(string IdentityToken) : ICommand<ExternalIdentityDto>;

public sealed class LinkExternalIdentityValidator : AbstractValidator<LinkExternalIdentityCommand>
{
    public LinkExternalIdentityValidator()
    {
        RuleFor(command => command.IdentityToken).NotEmpty().MaximumLength(16_384);
    }
}

public sealed class LinkExternalIdentityHandler(
    ICurrentPrincipal principal,
    IExternalIdentityTokenValidator tokenValidator,
    IAuthenticationRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<LinkExternalIdentityCommand, ExternalIdentityDto>
{
    public async Task<ExternalIdentityDto> Handle(LinkExternalIdentityCommand request, CancellationToken cancellationToken)
    {
        var userId = PrincipalGuards.RequireUser(principal);
        var verified = await tokenValidator.ValidateAsync(request.IdentityToken, cancellationToken);

        var existing = await repository.GetExternalIdentityAsync(
            verified.Issuer, verified.Subject, cancellationToken);
        if (existing is not null)
        {
            if (existing.UserId != userId)
            {
                throw new ConflictException("This identity is already linked to a different account.");
            }

            return ExternalIdentityDto.From(existing);
        }

        var identity = ExternalIdentity.Create(
            userId, verified.Issuer, verified.Subject, timeProvider.GetUtcNow());
        await repository.AddExternalIdentityAsync(identity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ExternalIdentityDto.From(identity);
    }
}

public sealed record ListLinkedIdentitiesQuery : IQuery<IReadOnlyList<ExternalIdentityDto>>;

public sealed class ListLinkedIdentitiesHandler(ICurrentPrincipal principal, IAuthenticationRepository repository)
    : IRequestHandler<ListLinkedIdentitiesQuery, IReadOnlyList<ExternalIdentityDto>>
{
    public async Task<IReadOnlyList<ExternalIdentityDto>> Handle(
        ListLinkedIdentitiesQuery request, CancellationToken cancellationToken)
    {
        var userId = PrincipalGuards.RequireUser(principal);
        var identities = await repository.ListExternalIdentitiesByUserAsync(userId, cancellationToken);
        return identities.Select(ExternalIdentityDto.From).ToArray();
    }
}

public sealed record UnlinkExternalIdentityCommand(Guid IdentityId) : ICommand<Unit>;

public sealed class UnlinkExternalIdentityHandler(
    ICurrentPrincipal principal,
    IAuthenticationRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<UnlinkExternalIdentityCommand, Unit>
{
    public async Task<Unit> Handle(UnlinkExternalIdentityCommand request, CancellationToken cancellationToken)
    {
        var userId = PrincipalGuards.RequireUser(principal);
        var identity = await repository.GetExternalIdentityAsync(request.IdentityId, userId, cancellationToken)
            ?? throw new NotFoundException("Linked identity was not found.");

        await repository.RemoveExternalIdentityAsync(identity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
