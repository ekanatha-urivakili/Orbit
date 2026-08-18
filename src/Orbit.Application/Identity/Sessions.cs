using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Common;
using Orbit.Domain.Identity;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Identity;

public sealed record AuthSessionDto(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    Guid SessionId,
    Guid UserId,
    string DisplayName,
    string Email,
    Guid WorkspaceId,
    string WorkspaceSlug,
    string WorkspaceName,
    TenantRole Role);

public sealed record SessionSummaryDto(
    Guid SessionId,
    Guid WorkspaceId,
    string WorkspaceName,
    string? UserAgent,
    string? IpAddress,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUsedAt,
    DateTimeOffset ExpiresAt,
    bool IsCurrent);

public sealed record AccountWorkspaceDto(
    Guid Id,
    string Slug,
    string Name,
    TenantRole Role);

internal static class RefreshTokenCodec
{
    public static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

internal static class MembershipSelection
{
    /// <summary>
    /// Picks the workspace context for a login/refresh: an explicit request wins, otherwise the
    /// session's current workspace is preserved, otherwise the account's earliest ("home")
    /// membership is used. Full default-workspace preference and multi-workspace switcher UX are
    /// tracked as follow-up work.
    /// </summary>
    public static TenantMembership? Select(
        IReadOnlyList<TenantMembership> memberships,
        Guid? requestedWorkspaceId,
        Guid? currentTenantId)
    {
        if (requestedWorkspaceId is { } requested)
        {
            return memberships.FirstOrDefault(membership => membership.TenantId == requested);
        }

        if (currentTenantId is { } current)
        {
            var existing = memberships.FirstOrDefault(membership => membership.TenantId == current);
            if (existing is not null)
            {
                return existing;
            }
        }

        return memberships.OrderBy(membership => membership.CreatedAt).FirstOrDefault();
    }
}

public sealed record LoginCommand(
    string Email,
    string Password,
    Guid? WorkspaceId,
    bool RememberMe,
    string? UserAgent,
    string? IpAddress) : ICommand<AuthSessionDto>;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(320);
        RuleFor(command => command.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class LoginHandler(
    IAuthenticationRepository repository,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<LoginCommand, AuthSessionDto>
{
    public async Task<AuthSessionDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = TryNormalizeEmail(request.Email);
        var account = normalizedEmail is null
            ? null
            : await repository.GetUserAccountByEmailAsync(normalizedEmail, cancellationToken);
        var credential = account is null
            ? null
            : await repository.GetLocalCredentialAsync(account.Id, cancellationToken);

        // Always run a verify of equivalent cost, even for an unknown email or missing local
        // credential, so response timing cannot be used to enumerate accounts (NFR-17).
        var passwordValid = await passwordHasher.VerifyAsync(
            request.Password,
            credential?.PasswordHash,
            cancellationToken);

        if (account is null
            || credential is null
            || !passwordValid
            || account.Status != UserAccountStatus.Active)
        {
            throw new AuthenticationException("Invalid email or password.");
        }

        var memberships = await repository.ListActiveMembershipsByUserAsync(account.Id, cancellationToken);
        var membership = MembershipSelection.Select(memberships, request.WorkspaceId, currentTenantId: null)
            ?? throw new AccessDeniedException("Your account has no active workspace membership.");
        var workspace = await repository.GetWorkspaceAsync(membership.TenantId, cancellationToken)
            ?? throw new NotFoundException("Workspace was not found.");

        var now = timeProvider.GetUtcNow();
        var refreshToken = RefreshTokenCodec.GenerateToken();
        var lifetime = request.RememberMe
            ? tokenIssuer.PersistentRefreshTokenLifetime
            : tokenIssuer.RefreshTokenLifetime;
        var session = RefreshSession.CreateInitial(
            account.Id,
            membership.TenantId,
            RefreshTokenCodec.Hash(refreshToken),
            request.UserAgent,
            request.IpAddress,
            request.RememberMe,
            now,
            lifetime);
        await repository.AddRefreshSessionAsync(session, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = tokenIssuer.IssueUserToken(account.Id, membership.TenantId, session.Id, now);
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

    private static string? TryNormalizeEmail(string email)
    {
        try
        {
            return UserAccount.NormalizeEmail(email);
        }
        catch (DomainException)
        {
            return null;
        }
    }
}

public sealed record RefreshSessionCommand(
    string RefreshToken,
    Guid? WorkspaceId,
    string? UserAgent,
    string? IpAddress) : ICommand<AuthSessionDto>;

public sealed class RefreshSessionValidator : AbstractValidator<RefreshSessionCommand>
{
    public RefreshSessionValidator()
    {
        RuleFor(command => command.RefreshToken).NotEmpty();
    }
}

public sealed class RefreshSessionHandler(
    IAuthenticationRepository repository,
    IAccessTokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<RefreshSessionCommand, AuthSessionDto>
{
    private const string InvalidSessionMessage = "The session is no longer valid.";

    public async Task<AuthSessionDto> Handle(RefreshSessionCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = RefreshTokenCodec.Hash(request.RefreshToken);
        var session = await repository.GetRefreshSessionByTokenHashAsync(tokenHash, cancellationToken)
            ?? throw new AuthenticationException(InvalidSessionMessage);

        var now = timeProvider.GetUtcNow();
        if (session.Status != RefreshSessionStatus.Active)
        {
            // The token was already rotated or revoked but is being presented again: treat this as
            // theft of a stolen refresh token and burn the entire rotation family (ADR-019 posture).
            await repository.RevokeFamilyAsync(session.FamilyId, now, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new AuthenticationException(InvalidSessionMessage);
        }

        if (!session.IsUsable(now))
        {
            throw new AuthenticationException(InvalidSessionMessage);
        }

        var memberships = await repository.ListActiveMembershipsByUserAsync(session.UserId, cancellationToken);
        var membership = MembershipSelection.Select(memberships, request.WorkspaceId, session.TenantId)
            ?? throw new AccessDeniedException("Your account has no active workspace membership.");
        var workspace = await repository.GetWorkspaceAsync(membership.TenantId, cancellationToken)
            ?? throw new NotFoundException("Workspace was not found.");
        var account = await repository.GetUserAccountAsync(session.UserId, cancellationToken)
            ?? throw new NotFoundException("User account was not found.");

        var refreshToken = RefreshTokenCodec.GenerateToken();
        var lifetime = session.IsPersistent
            ? tokenIssuer.PersistentRefreshTokenLifetime
            : tokenIssuer.RefreshTokenLifetime;
        var rotated = session.CreateRotated(
            membership.TenantId,
            RefreshTokenCodec.Hash(refreshToken),
            request.UserAgent,
            request.IpAddress,
            now,
            lifetime);
        session.MarkRotated(rotated.Id, now);
        await repository.AddRefreshSessionAsync(rotated, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = tokenIssuer.IssueUserToken(account.Id, membership.TenantId, rotated.Id, now);
        return new AuthSessionDto(
            accessToken.Value,
            accessToken.ExpiresAt,
            refreshToken,
            rotated.ExpiresAt,
            rotated.Id,
            account.Id,
            account.DisplayName,
            account.NormalizedEmail,
            workspace.Id,
            workspace.Slug,
            workspace.Name,
            membership.Role);
    }
}

public sealed record LogoutCommand(string RefreshToken) : ICommand<Unit>;

public sealed class LogoutValidator : AbstractValidator<LogoutCommand>
{
    public LogoutValidator()
    {
        RuleFor(command => command.RefreshToken).NotEmpty();
    }
}

public sealed class LogoutHandler(
    IAuthenticationRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<LogoutCommand, Unit>
{
    public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var session = await repository.GetRefreshSessionByTokenHashAsync(
            RefreshTokenCodec.Hash(request.RefreshToken),
            cancellationToken);
        if (session is not null)
        {
            session.Revoke(timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Unit.Value;
    }
}

public sealed record ListSessionsQuery : IQuery<IReadOnlyList<SessionSummaryDto>>;

public sealed record ListAccountWorkspacesQuery : IQuery<IReadOnlyList<AccountWorkspaceDto>>;

public sealed class ListAccountWorkspacesHandler(
    ICurrentPrincipal principal,
    IAuthenticationRepository repository)
    : IRequestHandler<ListAccountWorkspacesQuery, IReadOnlyList<AccountWorkspaceDto>>
{
    public async Task<IReadOnlyList<AccountWorkspaceDto>> Handle(
        ListAccountWorkspacesQuery request,
        CancellationToken cancellationToken)
    {
        var userId = PrincipalGuards.RequireUser(principal);
        var memberships = await repository.ListActiveMembershipsByUserAsync(userId, cancellationToken);
        var workspaces = (await repository.GetWorkspacesAsync(
                memberships.Select(membership => membership.TenantId).ToArray(),
                cancellationToken))
            .ToDictionary(workspace => workspace.Id);
        var result = new List<AccountWorkspaceDto>(memberships.Count);
        foreach (var membership in memberships)
        {
            if (workspaces.TryGetValue(membership.TenantId, out var workspace))
            {
                result.Add(new AccountWorkspaceDto(workspace.Id, workspace.Slug, workspace.Name, membership.Role));
            }
        }

        return result;
    }
}

public sealed class ListSessionsHandler(
    ICurrentPrincipal principal,
    IAuthenticationRepository repository) : IRequestHandler<ListSessionsQuery, IReadOnlyList<SessionSummaryDto>>
{
    public async Task<IReadOnlyList<SessionSummaryDto>> Handle(
        ListSessionsQuery request,
        CancellationToken cancellationToken)
    {
        var userId = PrincipalGuards.RequireUser(principal);
        var sessions = await repository.ListActiveSessionsByUserAsync(userId, cancellationToken);

        var tenantIds = sessions.Select(session => session.TenantId).Distinct().ToArray();
        var workspaces = (await repository.GetWorkspacesAsync(tenantIds, cancellationToken))
            .ToDictionary(workspace => workspace.Id);

        return sessions
            .OrderByDescending(session => session.LastUsedAt)
            .Select(session => new SessionSummaryDto(
                session.Id,
                session.TenantId,
                workspaces.TryGetValue(session.TenantId, out var workspace) ? workspace.Name : "Unknown workspace",
                session.UserAgent,
                session.IpAddress,
                session.CreatedAt,
                session.LastUsedAt,
                session.ExpiresAt,
                session.Id == principal.SessionId))
            .ToArray();
    }
}

public sealed record RevokeSessionCommand(Guid SessionId) : ICommand<Unit>;

public sealed class RevokeSessionHandler(
    ICurrentPrincipal principal,
    IAuthenticationRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<RevokeSessionCommand, Unit>
{
    public async Task<Unit> Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
    {
        var userId = PrincipalGuards.RequireUser(principal);
        var session = await repository.GetActiveSessionAsync(request.SessionId, userId, cancellationToken)
            ?? throw new NotFoundException("Session was not found.");

        session.Revoke(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record RevokeOtherSessionsCommand : ICommand<int>;

public sealed class RevokeOtherSessionsHandler(
    ICurrentPrincipal principal,
    IAuthenticationRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<RevokeOtherSessionsCommand, int>
{
    public async Task<int> Handle(RevokeOtherSessionsCommand request, CancellationToken cancellationToken)
    {
        var userId = PrincipalGuards.RequireUser(principal);
        var sessions = await repository.ListActiveSessionsByUserAsync(userId, cancellationToken);
        var now = timeProvider.GetUtcNow();

        var revoked = 0;
        foreach (var session in sessions)
        {
            if (session.Id == principal.SessionId)
            {
                continue;
            }

            session.Revoke(now);
            revoked++;
        }

        if (revoked > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return revoked;
    }
}
