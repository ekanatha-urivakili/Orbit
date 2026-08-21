using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Identity;
using Orbit.Domain.Organizations;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Identity;

internal static class GoogleOAuthConstants
{
    public const string Issuer = "https://accounts.google.com";
    public const string LinkMode = "link";
}

internal static class HandoffCodeCodec
{
    public static string GenerateCode() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    public static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}

/// <summary>
/// Starts the account-linking variant of the backend-brokered Google flow (ADR-020/v1.24): unlike
/// login/register, this requires an already-authenticated caller, whose id is embedded in the
/// signed <c>state</c> (delimited with '|' from the return URL) so the anonymous
/// <see cref="HandleGoogleCallbackHandler"/> callback - which runs with no bearer token, since
/// Google's own server redirect can't carry one - still knows which account to link into.
/// </summary>
public sealed record StartGoogleLinkCommand(string? ReturnUrl = null) : ICommand<string>;

public sealed class StartGoogleLinkHandler(
    ICurrentPrincipal principal,
    IGoogleOAuthClient client,
    IOAuthStateCodec stateCodec,
    TimeProvider timeProvider) : IRequestHandler<StartGoogleLinkCommand, string>
{
    public Task<string> Handle(StartGoogleLinkCommand request, CancellationToken cancellationToken)
    {
        var userId = PrincipalGuards.RequireUser(principal);
        var payload = $"{userId:D}|{request.ReturnUrl}";
        var state = stateCodec.Encode(
            GoogleOAuthConstants.LinkMode, timeProvider.GetUtcNow(), TimeSpan.FromMinutes(10), payload);
        return Task.FromResult(client.BuildAuthorizeUrl(state));
    }
}

public sealed record GoogleOAuthStartQuery(string Mode, string? ReturnUrl = null) : IQuery<GoogleOAuthStartDto>;

public sealed record GoogleOAuthStartDto(string AuthorizeUrl);

public sealed class GoogleOAuthStartValidator : AbstractValidator<GoogleOAuthStartQuery>
{
    public GoogleOAuthStartValidator() =>
        RuleFor(query => query.Mode).Must(mode => mode is "login" or "register")
            .WithMessage("Mode must be 'login' or 'register'.");
}

public sealed class GoogleOAuthStartHandler(
    IGoogleOAuthClient client,
    IOAuthStateCodec stateCodec,
    TimeProvider timeProvider) : IRequestHandler<GoogleOAuthStartQuery, GoogleOAuthStartDto>
{
    public Task<GoogleOAuthStartDto> Handle(GoogleOAuthStartQuery request, CancellationToken cancellationToken)
    {
        var state = stateCodec.Encode(request.Mode, timeProvider.GetUtcNow(), TimeSpan.FromMinutes(10), request.ReturnUrl);
        return Task.FromResult(new GoogleOAuthStartDto(client.BuildAuthorizeUrl(state)));
    }
}

public sealed record HandleGoogleCallbackCommand(string Code, string State) : ICommand<GoogleCallbackResultDto>;

/// <summary>
/// <paramref name="HandoffCode"/> is null when <paramref name="Linked"/> is true: linking an
/// already-authenticated caller's existing account needs no new session, only a redirect back to
/// the SPA confirming success.
/// </summary>
public sealed record GoogleCallbackResultDto(string? HandoffCode, string? ReturnUrl = null, bool Linked = false);

public sealed class HandleGoogleCallbackValidator : AbstractValidator<HandleGoogleCallbackCommand>
{
    public HandleGoogleCallbackValidator()
    {
        RuleFor(command => command.Code).NotEmpty();
        RuleFor(command => command.State).NotEmpty();
    }
}

/// <summary>
/// Resolves the OAuth code into a Google identity, then either logs in an already-linked/matching
/// account or provisions a brand-new organization for a first-time Google sign-in, and stores a
/// one-time <see cref="GoogleSignInHandoff"/> for the SPA to exchange for a real session
/// (<see cref="ExchangeGoogleHandoffHandler"/>) - this handler never itself returns tokens, since it
/// runs inside a server-side browser redirect.
/// </summary>
public sealed class HandleGoogleCallbackHandler(
    IGoogleOAuthClient client,
    IGoogleIdTokenValidator idTokenValidator,
    IOAuthStateCodec stateCodec,
    IAuthenticationRepository authenticationRepository,
    ISignUpRepository signUpRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<HandleGoogleCallbackCommand, GoogleCallbackResultDto>
{
    public async Task<GoogleCallbackResultDto> Handle(
        HandleGoogleCallbackCommand request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (!stateCodec.TryDecode(request.State, now, out var mode, out var returnUrl))
        {
            throw new AuthenticationException("The sign-in request expired or is invalid. Please try again.");
        }

        var idToken = await client.ExchangeCodeForIdTokenAsync(request.Code, cancellationToken);
        var identity = await idTokenValidator.ValidateAsync(idToken, cancellationToken);

        if (mode == GoogleOAuthConstants.LinkMode)
        {
            return await LinkExistingAccountAsync(identity, returnUrl, now, cancellationToken);
        }

        var existingIdentity = await authenticationRepository.GetExternalIdentityAsync(
            GoogleOAuthConstants.Issuer, identity.Subject, cancellationToken);

        if (existingIdentity is not null)
        {
            var tenantId = await SelectTenantAsync(existingIdentity.UserId, cancellationToken);
            return new GoogleCallbackResultDto(
                await CreateHandoffAsync(existingIdentity.UserId, tenantId, now, cancellationToken),
                returnUrl);
        }

        if (identity.Email is not null && identity.EmailVerified)
        {
            var normalizedEmail = UserAccount.NormalizeEmail(identity.Email);
            var existingAccount = await authenticationRepository.GetUserAccountByEmailAsync(normalizedEmail, cancellationToken);
            if (existingAccount is not null)
            {
                var externalIdentity = ExternalIdentity.Create(existingAccount.Id, GoogleOAuthConstants.Issuer, identity.Subject, now);
                await authenticationRepository.AddExternalIdentityAsync(externalIdentity, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var tenantId = await SelectTenantAsync(existingAccount.Id, cancellationToken);
                return new GoogleCallbackResultDto(
                    await CreateHandoffAsync(existingAccount.Id, tenantId, now, cancellationToken),
                    returnUrl);
            }
        }

        // Auto-provision brand-new account, organization, and workspace when no matching account exists yet.
        return new GoogleCallbackResultDto(
            await ProvisionNewAccountAsync(identity, now, cancellationToken),
            returnUrl);
    }

    /// <summary>
    /// Completes <see cref="StartGoogleLinkHandler"/>'s flow: <paramref name="statePayload"/> is the
    /// combined <c>{userId}|{returnUrl}</c> string embedded at start time (see the '|'-delimited
    /// format there), since the anonymous callback has no bearer token to identify the caller with.
    /// Reuses <see cref="LinkExternalIdentityCommand"/>'s same-email-or-unclaimed security rule
    /// (§Identity/ExternalIdentities.cs) rather than inventing a separate one.
    /// </summary>
    private async Task<GoogleCallbackResultDto> LinkExistingAccountAsync(
        VerifiedGoogleIdentity identity,
        string? statePayload,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var parts = (statePayload ?? string.Empty).Split('|', 2);
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var userId))
        {
            throw new AuthenticationException("The linking request expired or is invalid. Please try again.");
        }

        var returnUrl = string.IsNullOrEmpty(parts[1]) ? null : parts[1];

        var account = await authenticationRepository.GetUserAccountAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Account was not found.");

        if (identity.EmailVerified && identity.Email is { } claimedEmail
            && !string.Equals(UserAccount.NormalizeEmail(claimedEmail), account.NormalizedEmail, StringComparison.Ordinal))
        {
            throw new ConflictException("The Google account's verified email does not match this account's email.");
        }

        var existing = await authenticationRepository.GetExternalIdentityAsync(
            GoogleOAuthConstants.Issuer, identity.Subject, cancellationToken);
        if (existing is not null)
        {
            if (existing.UserId != userId)
            {
                throw new ConflictException("This Google account is already linked to a different account.");
            }

            return new GoogleCallbackResultDto(HandoffCode: null, returnUrl, Linked: true);
        }

        var externalIdentity = ExternalIdentity.Create(userId, GoogleOAuthConstants.Issuer, identity.Subject, now);
        await authenticationRepository.AddExternalIdentityAsync(externalIdentity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new GoogleCallbackResultDto(HandoffCode: null, returnUrl, Linked: true);
    }

    private async Task<Guid> SelectTenantAsync(Guid userId, CancellationToken cancellationToken)
    {
        var memberships = await authenticationRepository.ListActiveMembershipsByUserAsync(userId, cancellationToken);
        var membership = MembershipSelection.Select(memberships, requestedWorkspaceId: null, currentTenantId: null)
            ?? throw new AccessDeniedException("Your account has no active workspace membership.");
        return membership.TenantId;
    }

    private async Task<string> ProvisionNewAccountAsync(
        VerifiedGoogleIdentity identity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (identity.Email is null)
        {
            throw new AuthenticationException("Your Google account has no email address to sign up with.");
        }

        var displayName = string.IsNullOrWhiteSpace(identity.Name) ? identity.Email : identity.Name;
        var account = UserAccount.Create(identity.Email, displayName, now);
        var externalIdentity = ExternalIdentity.Create(account.Id, GoogleOAuthConstants.Issuer, identity.Subject, now);
        var organization = Organization.Create($"{displayName}'s Organization", now);
        var workspace = Workspace.Create(organization.Id, $"{displayName}'s Workspace", now);
        var organizationMembership = OrganizationMembership.Create(
            organization.Id, account.Id, OrganizationRole.Owner, now);
        var ownerMembership = TenantMembership.CreateForUser(workspace.Id, account.Id, TenantRole.Owner, now);

        var code = HandoffCodeCodec.GenerateCode();
        var handoff = GoogleSignInHandoff.Create(HandoffCodeCodec.Hash(code), account.Id, workspace.Id, now);

        await signUpRepository.ProvisionExternalAccountAsync(
            account, externalIdentity, organization, workspace, organizationMembership, ownerMembership, handoff, cancellationToken);

        return code;
    }

    private async Task<string> CreateHandoffAsync(
        Guid userId,
        Guid tenantId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var code = HandoffCodeCodec.GenerateCode();
        var handoff = GoogleSignInHandoff.Create(HandoffCodeCodec.Hash(code), userId, tenantId, now);
        await authenticationRepository.AddSignInHandoffAsync(handoff, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return code;
    }
}

public sealed record ExchangeGoogleHandoffCommand(string Code) : ICommand<AuthSessionDto>;

public sealed class ExchangeGoogleHandoffValidator : AbstractValidator<ExchangeGoogleHandoffCommand>
{
    public ExchangeGoogleHandoffValidator() =>
        RuleFor(command => command.Code).NotEmpty();
}

public sealed class ExchangeGoogleHandoffHandler(
    IAuthenticationRepository repository,
    IAccessTokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<ExchangeGoogleHandoffCommand, AuthSessionDto>
{
    public async Task<AuthSessionDto> Handle(ExchangeGoogleHandoffCommand request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var handoff = await repository.ConsumeSignInHandoffAsync(HandoffCodeCodec.Hash(request.Code), now, cancellationToken)
            ?? throw new AuthenticationException("The sign-in request expired or was already used. Please try again.");

        var account = await repository.GetUserAccountAsync(handoff.UserId, cancellationToken)
            ?? throw new NotFoundException("User account was not found.");
        var workspace = await repository.GetWorkspaceAsync(handoff.TenantId, cancellationToken)
            ?? throw new NotFoundException("Workspace was not found.");
        var memberships = await repository.ListActiveMembershipsByUserAsync(account.Id, cancellationToken);
        var membership = memberships.FirstOrDefault(candidate => candidate.TenantId == handoff.TenantId)
            ?? throw new AccessDeniedException("Your account has no active workspace membership.");

        var refreshToken = RefreshTokenCodec.GenerateToken();
        var session = RefreshSession.CreateInitial(
            account.Id,
            workspace.Id,
            RefreshTokenCodec.Hash(refreshToken),
            userAgent: null,
            ipAddress: null,
            isPersistent: false,
            now,
            tokenIssuer.RefreshTokenLifetime);
        await repository.AddRefreshSessionAsync(session, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = tokenIssuer.IssueUserToken(account.Id, workspace.Id, session.Id, now);
        return new AuthSessionDto(
            accessToken.Value,
            accessToken.ExpiresAt,
            refreshToken,
            session.ExpiresAt,
            session.Id,
            account.Id,
            account.DisplayName,
            account.NormalizedEmail,
            workspace.Id,
            workspace.Slug,
            workspace.Name,
            membership.Role);
    }
}
