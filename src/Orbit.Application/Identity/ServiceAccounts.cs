using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Identity;

namespace Orbit.Application.Identity;

public sealed record ServiceAccountCredentialDto(Guid MembershipId, string ClientId, string ClientSecret, TenantRole Role);

/// <summary>
/// Creates a service-account <see cref="TenantMembership"/> and its first <see cref="ServiceAccountCredential"/>
/// atomically - closing the "no way to actually mint a token for a service account" gap:
/// <c>POST /memberships</c> could already create the membership row by hand, but nothing could
/// issue a bearer token that would satisfy TenantTransactionMiddleware's expectations for it.
/// </summary>
public sealed record CreateServiceAccountCommand(TenantRole Role) : ICommand<ServiceAccountCredentialDto>;

public sealed class CreateServiceAccountValidator : AbstractValidator<CreateServiceAccountCommand>
{
    public CreateServiceAccountValidator()
    {
        RuleFor(command => command.Role).IsInEnum().NotEqual(TenantRole.Owner);
    }
}

public sealed class CreateServiceAccountHandler(
    ITenantContext tenantContext,
    ITenantAuthorization authorization,
    ITenantMembershipRepository memberships,
    IAuthenticationRepository authentication,
    IAccessTokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<CreateServiceAccountCommand, ServiceAccountCredentialDto>
{
    public async Task<ServiceAccountCredentialDto> Handle(
        CreateServiceAccountCommand request,
        CancellationToken cancellationToken)
    {
        if (!authorization.CanCreateMembership(request.Role))
        {
            throw new AccessDeniedException("The current principal cannot grant this tenant role.");
        }

        var now = timeProvider.GetUtcNow();
        var clientId = Guid.CreateVersion7();
        var membership = TenantMembership.Create(
            tenantContext.TenantId,
            tokenIssuer.LocalIssuer,
            clientId.ToString(),
            PrincipalType.ServiceAccount,
            request.Role,
            now);
        await memberships.AddAsync(membership, cancellationToken);

        var rawSecret = RefreshTokenCodec.GenerateToken();
        var credential = ServiceAccountCredential.Create(
            tenantContext.TenantId, membership.Id, clientId, RefreshTokenCodec.Hash(rawSecret), now);
        await authentication.AddServiceAccountCredentialAsync(credential, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceAccountCredentialDto(membership.Id, clientId.ToString(), rawSecret, request.Role);
    }
}

/// <summary>
/// Revokes any existing active credential(s) for a service account and issues a new one - the raw
/// secret is only ever returned here and at creation, never again.
/// </summary>
public sealed record RotateServiceAccountCredentialCommand(Guid MembershipId) : ICommand<ServiceAccountCredentialDto>;

public sealed class RotateServiceAccountCredentialHandler(
    ITenantContext tenantContext,
    ITenantAuthorization authorization,
    ITenantMembershipRepository memberships,
    IAuthenticationRepository authentication,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<RotateServiceAccountCredentialCommand, ServiceAccountCredentialDto>
{
    public async Task<ServiceAccountCredentialDto> Handle(
        RotateServiceAccountCredentialCommand request,
        CancellationToken cancellationToken)
    {
        if (!authorization.CanManageTeams())
        {
            throw new AccessDeniedException("Workspace administration permission is required.");
        }

        var membership = await memberships.GetActiveAsync(tenantContext.TenantId, request.MembershipId, cancellationToken);
        if (membership is null
            || membership.PrincipalType != PrincipalType.ServiceAccount
            || !Guid.TryParse(membership.Subject, out var clientId))
        {
            throw new NotFoundException("Service account was not found.");
        }

        var now = timeProvider.GetUtcNow();
        var existingCredentials = await authentication.ListActiveServiceAccountCredentialsByMembershipAsync(
            membership.Id, cancellationToken);
        foreach (var credential in existingCredentials)
        {
            credential.Revoke(now);
        }

        var rawSecret = RefreshTokenCodec.GenerateToken();
        var newCredential = ServiceAccountCredential.Create(
            tenantContext.TenantId, membership.Id, clientId, RefreshTokenCodec.Hash(rawSecret), now);
        await authentication.AddServiceAccountCredentialAsync(newCredential, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServiceAccountCredentialDto(membership.Id, clientId.ToString(), rawSecret, membership.Role);
    }
}

public sealed record AccessTokenDto(string AccessToken, DateTimeOffset ExpiresAt, string TokenType = "Bearer");

public sealed record IssueServiceAccountTokenCommand(string ClientId, string ClientSecret) : ICommand<AccessTokenDto>;

public sealed class IssueServiceAccountTokenValidator : AbstractValidator<IssueServiceAccountTokenCommand>
{
    public IssueServiceAccountTokenValidator()
    {
        RuleFor(command => command.ClientId).NotEmpty().MaximumLength(64);
        RuleFor(command => command.ClientSecret).NotEmpty().MaximumLength(128);
    }
}

/// <summary>
/// The client-credentials token endpoint: pre-auth, like login. Resolves the credential globally by
/// client id (no ambient tenant context exists yet), verifies the secret, then re-establishes
/// <c>app.tenant_id</c> from the credential's own tenant to look up the still-active membership -
/// see <see cref="IAuthenticationRepository.GetActiveServiceAccountMembershipAsync"/>.
/// </summary>
public sealed class IssueServiceAccountTokenHandler(
    IAuthenticationRepository authentication,
    IAccessTokenIssuer tokenIssuer,
    TimeProvider timeProvider) : IRequestHandler<IssueServiceAccountTokenCommand, AccessTokenDto>
{
    private const string InvalidCredentialMessage = "The client id or client secret is invalid.";

    public async Task<AccessTokenDto> Handle(
        IssueServiceAccountTokenCommand request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.ClientId, out var clientId))
        {
            throw new AuthenticationException(InvalidCredentialMessage);
        }

        var credential = await authentication.GetActiveServiceAccountCredentialByClientIdAsync(clientId, cancellationToken);
        if (credential is null || credential.SecretHash != RefreshTokenCodec.Hash(request.ClientSecret))
        {
            throw new AuthenticationException(InvalidCredentialMessage);
        }

        var membership = await authentication.GetActiveServiceAccountMembershipAsync(
            credential.TenantId, credential.MembershipId, cancellationToken);
        if (membership is null)
        {
            throw new AuthenticationException(InvalidCredentialMessage);
        }

        var now = timeProvider.GetUtcNow();
        var token = tokenIssuer.IssueServiceAccountToken(credential.TenantId, credential.ClientId.ToString(), now);
        return new AccessTokenDto(token.Value, token.ExpiresAt);
    }
}
