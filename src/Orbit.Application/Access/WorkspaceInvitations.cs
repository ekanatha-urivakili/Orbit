using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Directory;
using Orbit.Domain.Identity;
using Orbit.Domain.Messaging;

namespace Orbit.Application.Access;

public sealed record WorkspaceInvitationDto(
    Guid Id,
    string Email,
    TenantRole Role,
    Guid? TeamId,
    WorkspaceInvitationStatus Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AcceptedAt)
{
    public static WorkspaceInvitationDto From(WorkspaceInvitation invitation) =>
        new(
            invitation.Id,
            invitation.NormalizedEmail,
            invitation.Role,
            invitation.TeamId,
            invitation.Status,
            invitation.ExpiresAt,
            invitation.CreatedAt,
            invitation.AcceptedAt);
}

public sealed record CreateWorkspaceInvitationCommand(
    string Email,
    TenantRole Role,
    Guid? TeamId,
    string FrontendBaseUrl) : ICommand<WorkspaceInvitationDto>;

public sealed class CreateWorkspaceInvitationValidator : AbstractValidator<CreateWorkspaceInvitationCommand>
{
    public CreateWorkspaceInvitationValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(320).EmailAddress();
        RuleFor(command => command.Role).IsInEnum().NotEqual(TenantRole.Owner);
        RuleFor(command => command.TeamId).NotEmpty().When(command => command.TeamId.HasValue);
        RuleFor(command => command.FrontendBaseUrl)
            .Must(value =>
                Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https"
                && string.IsNullOrEmpty(uri.Query)
                && string.IsNullOrEmpty(uri.Fragment))
            .WithMessage("Frontend base URL must be an absolute HTTP or HTTPS URL without a query or fragment.");
    }
}

public sealed class CreateWorkspaceInvitationHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    ITenantAuthorization authorization,
    IWorkspaceInvitationRepository invitations,
    ITeamRepository teams,
    ISettingsRepository settings,
    IOutboxRepository outbox,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<CreateWorkspaceInvitationCommand, WorkspaceInvitationDto>
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    public async Task<WorkspaceInvitationDto> Handle(
        CreateWorkspaceInvitationCommand request,
        CancellationToken cancellationToken)
    {
        if (!authorization.CanCreateMembership(request.Role))
        {
            throw new AccessDeniedException("The current principal cannot grant this workspace role.");
        }

        var tenantId = tenantContext.TenantId;
        if (request.TeamId is { } teamId)
        {
            _ = await teams.GetAsync(tenantId, teamId, cancellationToken)
                ?? throw new NotFoundException("Team was not found.");
        }

        var workspace = await settings.GetWorkspaceAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException("Workspace was not found.");
        var normalizedEmail = UserAccount.NormalizeEmail(request.Email);
        var tokenHash = InvitationTokenCodec.Hash(InvitationTokenCodec.Generate());
        var now = timeProvider.GetUtcNow();
        var invitation = await invitations.GetActiveByEmailAsync(tenantId, normalizedEmail, cancellationToken);
        if (invitation is null)
        {
            invitation = WorkspaceInvitation.Create(
                tenantId,
                normalizedEmail,
                request.Role,
                request.TeamId,
                tokenHash,
                principal.MembershipId,
                now,
                InvitationLifetime);
            await invitations.AddAsync(invitation, cancellationToken);
        }
        else
        {
            invitation.Renew(
                request.Role,
                request.TeamId,
                tokenHash,
                principal.MembershipId,
                now,
                InvitationLifetime);
        }

        var message = OutboxEmailMessage.CreateWorkspaceInvitation(
            normalizedEmail,
            $"Join {workspace.Name} on Orbit",
            tenantId,
            invitation.Id,
            request.FrontendBaseUrl,
            now);
        await outbox.AddAsync(message, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkspaceInvitationDto.From(invitation);
    }
}

public sealed record ListWorkspaceInvitationsQuery(
    string? Email = null,
    WorkspaceInvitationStatus? Status = null) : IQuery<IReadOnlyList<WorkspaceInvitationDto>>;

public sealed class ListWorkspaceInvitationsHandler(
    ITenantContext tenantContext,
    ITenantAuthorization authorization,
    IWorkspaceInvitationRepository invitations)
    : IRequestHandler<ListWorkspaceInvitationsQuery, IReadOnlyList<WorkspaceInvitationDto>>
{
    public async Task<IReadOnlyList<WorkspaceInvitationDto>> Handle(
        ListWorkspaceInvitationsQuery request,
        CancellationToken cancellationToken)
    {
        if (!authorization.CanManageTeams())
        {
            throw new AccessDeniedException("The current principal cannot view workspace invitations.");
        }

        // A plain lowercase trim, not UserAccount.NormalizeEmail: this is a partial search term
        // (e.g. "alice"), not necessarily a complete address, so the strict "user@domain" shape
        // that NormalizeEmail requires would throw on it.
        var emailSearch = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : request.Email.Trim().ToLowerInvariant();
        return (await invitations.ListAsync(tenantContext.TenantId, emailSearch, request.Status, cancellationToken))
            .Select(WorkspaceInvitationDto.From)
            .ToArray();
    }
}

public sealed record RevokeWorkspaceInvitationCommand(Guid InvitationId) : ICommand<Unit>;

public sealed class RevokeWorkspaceInvitationHandler(
    ITenantContext tenantContext,
    ITenantAuthorization authorization,
    IWorkspaceInvitationRepository invitations,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<RevokeWorkspaceInvitationCommand, Unit>
{
    public async Task<Unit> Handle(RevokeWorkspaceInvitationCommand request, CancellationToken cancellationToken)
    {
        if (!authorization.CanManageTeams())
        {
            throw new AccessDeniedException("The current principal cannot revoke workspace invitations.");
        }

        var invitation = await invitations.GetAsync(
            tenantContext.TenantId,
            request.InvitationId,
            cancellationToken) ?? throw new NotFoundException("Invitation was not found.");
        invitation.Revoke(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record AcceptWorkspaceInvitationCommand(
    string Token,
    string DisplayName,
    string Password) : ICommand<TenantMembershipDto>;

public sealed class AcceptWorkspaceInvitationValidator : AbstractValidator<AcceptWorkspaceInvitationCommand>
{
    public AcceptWorkspaceInvitationValidator()
    {
        RuleFor(command => command.Token).NotEmpty().MaximumLength(128);
        RuleFor(command => command.DisplayName).NotEmpty().Length(2, 120);
        RuleFor(command => command.Password)
            .NotEmpty()
            .Length(12, 128)
            .Must(password => password.Any(char.IsLower)).WithMessage("Password must contain a lowercase letter.")
            .Must(password => password.Any(char.IsUpper)).WithMessage("Password must contain an uppercase letter.")
            .Must(password => password.Any(char.IsDigit)).WithMessage("Password must contain a number.");
    }
}

public sealed class AcceptWorkspaceInvitationHandler(
    ITenantContext tenantContext,
    IWorkspaceInvitationRepository invitations,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<AcceptWorkspaceInvitationCommand, TenantMembershipDto>
{
    private const string InvalidInvitationMessage = "The invitation is invalid, expired, or does not match your account.";

    public async Task<TenantMembershipDto> Handle(
        AcceptWorkspaceInvitationCommand request,
        CancellationToken cancellationToken)
    {
        var tokenHash = InvitationTokenCodec.Hash(request.Token);
        var invitation = await invitations.GetByTokenHashAsync(
            tenantContext.TenantId,
            tokenHash,
            cancellationToken) ?? throw new AuthenticationException(InvalidInvitationMessage);
        var now = timeProvider.GetUtcNow();
        if (!invitation.IsUsable(now))
        {
            throw new AuthenticationException(InvalidInvitationMessage);
        }

        var account = await invitations.GetUserAccountByEmailAsync(
            invitation.NormalizedEmail,
            cancellationToken);
        if (account is null)
        {
            account = UserAccount.Create(invitation.NormalizedEmail, request.DisplayName, now);
            var passwordHash = await passwordHasher.HashAsync(request.Password, cancellationToken);
            var credential = LocalCredential.Create(
                account.Id,
                passwordHash.Value,
                passwordHash.Algorithm,
                passwordHash.ParametersVersion,
                now);
            await invitations.AddUserAccountAsync(account, cancellationToken);
            await invitations.AddLocalCredentialAsync(credential, cancellationToken);
        }
        else
        {
            var credential = await invitations.GetUserAccountCredentialAsync(account.Id, cancellationToken);
            var validPassword = await passwordHasher.VerifyAsync(
                request.Password,
                credential?.PasswordHash,
                cancellationToken);
            if (!validPassword || credential is null || account.Status != UserAccountStatus.Active)
            {
                throw new AuthenticationException(InvalidInvitationMessage);
            }
        }

        var membership = await EnsureMembershipAsync(invitations, invitation, account.Id, now, cancellationToken);
        invitation.Accept(account.Id, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TenantMembershipDto.From(membership);
    }

    internal static async Task<TenantMembership> EnsureMembershipAsync(
        IWorkspaceInvitationRepository invitations,
        WorkspaceInvitation invitation,
        Guid accountId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var membership = await invitations.GetMembershipByUserAsync(
            invitation.TenantId,
            accountId,
            cancellationToken);
        if (membership is null)
        {
            membership = TenantMembership.CreateForUser(invitation.TenantId, accountId, invitation.Role, now);
            await invitations.AddTenantMembershipAsync(membership, cancellationToken);
        }
        else if (!membership.IsActive)
        {
            membership.Reactivate(invitation.Role);
        }
        else if (membership.Role != invitation.Role)
        {
            membership.ChangeRole(invitation.Role);
        }

        if (invitation.TeamId is { } teamId
            && await invitations.GetTeamMembershipAsync(
                invitation.TenantId,
                teamId,
                membership.Id,
                cancellationToken) is null)
        {
            await invitations.AddTeamMembershipAsync(
                TeamMembership.Create(invitation.TenantId, teamId, membership.Id, now),
                cancellationToken);
        }

        return membership;
    }
}

public sealed record AcceptWorkspaceInvitationWithExternalIdentityCommand(
    string Token,
    string ExternalIdToken,
    string DisplayName) : ICommand<TenantMembershipDto>;

public sealed class AcceptWorkspaceInvitationWithExternalIdentityValidator
    : AbstractValidator<AcceptWorkspaceInvitationWithExternalIdentityCommand>
{
    public AcceptWorkspaceInvitationWithExternalIdentityValidator()
    {
        RuleFor(command => command.Token).NotEmpty().MaximumLength(128);
        RuleFor(command => command.ExternalIdToken).NotEmpty().MaximumLength(16_384);
        RuleFor(command => command.DisplayName).NotEmpty().Length(2, 120);
    }
}

/// <summary>
/// Accepts a workspace invitation by proving control of the invited email through an external OIDC
/// identity instead of setting a local password - the resulting account (when newly created) has no
/// <see cref="LocalCredential"/> at all, so it can only ever sign in via that external identity.
/// </summary>
public sealed class AcceptWorkspaceInvitationWithExternalIdentityHandler(
    ITenantContext tenantContext,
    IWorkspaceInvitationRepository invitations,
    IAuthenticationRepository authentication,
    IExternalIdentityTokenValidator tokenValidator,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<AcceptWorkspaceInvitationWithExternalIdentityCommand, TenantMembershipDto>
{
    private const string InvalidInvitationMessage = "The invitation is invalid, expired, or does not match your account.";

    public async Task<TenantMembershipDto> Handle(
        AcceptWorkspaceInvitationWithExternalIdentityCommand request,
        CancellationToken cancellationToken)
    {
        var tokenHash = InvitationTokenCodec.Hash(request.Token);
        var invitation = await invitations.GetByTokenHashAsync(
            tenantContext.TenantId,
            tokenHash,
            cancellationToken) ?? throw new AuthenticationException(InvalidInvitationMessage);
        var now = timeProvider.GetUtcNow();
        if (!invitation.IsUsable(now))
        {
            throw new AuthenticationException(InvalidInvitationMessage);
        }

        var verified = await tokenValidator.ValidateAsync(request.ExternalIdToken, cancellationToken);
        if (!verified.EmailVerified
            || verified.Email is null
            || !string.Equals(UserAccount.NormalizeEmail(verified.Email), invitation.NormalizedEmail, StringComparison.Ordinal))
        {
            throw new AuthenticationException(InvalidInvitationMessage);
        }

        var existingIdentity = await authentication.GetExternalIdentityAsync(
            verified.Issuer, verified.Subject, cancellationToken);
        UserAccount account;
        if (existingIdentity is not null)
        {
            account = await authentication.GetUserAccountAsync(existingIdentity.UserId, cancellationToken)
                ?? throw new AuthenticationException(InvalidInvitationMessage);
            if (!string.Equals(account.NormalizedEmail, invitation.NormalizedEmail, StringComparison.Ordinal)
                || account.Status != UserAccountStatus.Active)
            {
                throw new AuthenticationException(InvalidInvitationMessage);
            }
        }
        else
        {
            account = UserAccount.Create(invitation.NormalizedEmail, request.DisplayName, now);
            await invitations.AddUserAccountAsync(account, cancellationToken);
            await authentication.AddExternalIdentityAsync(
                ExternalIdentity.Create(account.Id, verified.Issuer, verified.Subject, now),
                cancellationToken);
        }

        var membership = await AcceptWorkspaceInvitationHandler.EnsureMembershipAsync(
            invitations, invitation, account.Id, now, cancellationToken);
        invitation.Accept(account.Id, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TenantMembershipDto.From(membership);
    }
}

public static class InvitationTokenCodec
{
    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
