using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Access;
using Orbit.Infrastructure.Authorization;
using Orbit.Infrastructure.Identity;
using Orbit.Infrastructure.Persistence;

namespace Orbit.Api.Tenancy;

public sealed class TenantTransactionMiddleware(RequestDelegate next)
{
    private const string TenantHeader = "X-Tenant-Id";

    public async Task InvokeAsync(
        HttpContext context,
        TenantContext tenantContext,
        CurrentPrincipal currentPrincipal,
        ITenantMembershipRepository memberships,
        ISettingsRepository settings,
        IAuthorizationContextCache authorizationCache,
        IAuthenticationRepository authentication,
        OrbitDbContext dbContext,
        IConfiguration configuration)
    {
        if (!RequiresTenant(context))
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var identity = ResolveIdentity(context.User);
                if (identity is { } id)
                {
                    var sessionId = Guid.TryParse(context.User.FindFirstValue("sid"), out var parsedSessionId)
                        ? parsedSessionId
                        : (Guid?)null;

                    Guid? userId = null;
                    if (id.PrincipalType == PrincipalType.User)
                    {
                        var localIssuer = configuration[$"{LocalTokenOptions.SectionName}:Issuer"]
                            ?? LocalTokenOptions.DefaultIssuer;
                        if (string.Equals(id.Issuer, localIssuer, StringComparison.Ordinal)
                            && Guid.TryParse(id.Subject, out var localUserId))
                        {
                            userId = localUserId;
                        }
                        else
                        {
                            var externalIdentity = await authentication.GetExternalIdentityAsync(
                                id.Issuer, id.Subject, context.RequestAborted);
                            userId = externalIdentity?.UserId;
                        }
                    }

                    if (userId is { } resolvedUserId)
                    {
                        currentPrincipal.SetUser(resolvedUserId, id.PrincipalType, sessionId);
                    }
                }
            }

            await next(context);
            return;
        }

        var publicInvitation = IsPublicInvitation(context.Request.Path);
        var allowHeader = configuration.GetValue<bool>("Tenancy:AllowHeaderTenant");
        var tenantValue = publicInvitation
            ? context.Request.RouteValues["tenantId"]?.ToString()
            : allowHeader
                ? context.Request.Headers[TenantHeader].ToString()
                : context.User.FindFirstValue("tenant_id");

        if (!Guid.TryParse(tenantValue, out var tenantId) || tenantId == Guid.Empty)
        {
            await Results.Problem(
                statusCode: allowHeader ? StatusCodes.Status400BadRequest : StatusCodes.Status401Unauthorized,
                type: "/problems/invalid-tenant",
                title: allowHeader
                    ? "A valid X-Tenant-Id header is required."
                    : "An authenticated tenant claim is required.")
                .ExecuteAsync(context);
            return;
        }

        tenantContext.SetTenant(tenantId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(context.RequestAborted);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.tenant_id', {tenantId.ToString()}, true)",
            context.RequestAborted);

        if (publicInvitation)
        {
            try
            {
                await next(context);
                await transaction.CommitAsync(context.RequestAborted);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }

            return;
        }

        if (allowHeader)
        {
            var owner = await memberships.GetOwnerAsync(tenantId, context.RequestAborted);
            if (owner is null)
            {
                currentPrincipal.SetDevelopmentPrincipal(tenantId);
            }
            else
            {
                currentPrincipal.SetMembership(owner);
            }
        }
        else
        {
            var identity = ResolveIdentity(context.User);
            if (identity is null)
            {
                await UnauthorizedAsync(context, "A valid subject and issuer are required.");
                return;
            }

            var sessionId = Guid.TryParse(context.User.FindFirstValue("sid"), out var parsedSessionId)
                ? parsedSessionId
                : (Guid?)null;

            // A locally-issued access token carries the account's own user id as the subject
            // (see JwtAccessTokenIssuer). An externally validated OIDC token (identity.PrincipalType
            // User, opaque subject) resolves through a linked ExternalIdentity to that same global
            // account when one exists; otherwise it falls back to the older tenant-scoped federated
            // membership match (issuer/subject) used for admin-provisioned members with no local
            // account, and service-account principals always use that federated match.
            Guid? userId = null;
            if (identity.Value.PrincipalType == PrincipalType.User)
            {
                var localIssuer = configuration[$"{LocalTokenOptions.SectionName}:Issuer"]
                    ?? LocalTokenOptions.DefaultIssuer;
                if (string.Equals(identity.Value.Issuer, localIssuer, StringComparison.Ordinal)
                    && Guid.TryParse(identity.Value.Subject, out var localUserId))
                {
                    userId = localUserId;
                }
                else
                {
                    var externalIdentity = await authentication.GetExternalIdentityAsync(
                        identity.Value.Issuer, identity.Value.Subject, context.RequestAborted);
                    userId = externalIdentity?.UserId;
                }
            }

            if (userId is { } resolvedUserId)
            {
                var resolved = await ResolveUserPrincipalAsync(
                    tenantId, resolvedUserId, memberships, settings, authorizationCache, context.RequestAborted);
                if (resolved is null)
                {
                    await UnauthorizedAsync(context, "The principal is not an active member of this tenant.");
                    return;
                }

                currentPrincipal.SetMembership(
                    resolved.MembershipId,
                    resolved.UserId,
                    resolved.PrincipalType,
                    resolved.TenantRole,
                    resolved.MembershipTier,
                    sessionId);
            }
            else
            {
                var membership = await memberships.GetActiveAsync(
                    tenantId, identity.Value.Issuer, identity.Value.Subject, context.RequestAborted);
                if (membership is null || membership.PrincipalType != identity.Value.PrincipalType)
                {
                    await UnauthorizedAsync(context, "The principal is not an active member of this tenant.");
                    return;
                }

                currentPrincipal.SetMembership(membership, sessionId);
            }
        }

        // SEC-04: Explicit rollback on any exception prevents partial DB writes from
        // being committed by the driver's implicit flush when the transaction is disposed.
        try
        {
            await next(context);
            await transaction.CommitAsync(context.RequestAborted);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Resolves the active membership for a locally-issued user token, reading through
    /// <see cref="IAuthorizationContextCache"/> keyed by the workspace's current authorization
    /// epoch. A cache miss falls back to <see cref="ITenantMembershipRepository.GetActiveByUserAsync"/>
    /// and writes the result back under the current epoch.
    /// </summary>
    private static async Task<CachedAuthorizationContext?> ResolveUserPrincipalAsync(
        Guid tenantId,
        Guid userId,
        ITenantMembershipRepository memberships,
        ISettingsRepository settings,
        IAuthorizationContextCache authorizationCache,
        CancellationToken cancellationToken)
    {
        var workspace = await settings.GetWorkspaceAsync(tenantId, cancellationToken);
        if (workspace is null)
        {
            return null;
        }

        var cached = await authorizationCache.GetAsync(
            tenantId, userId, workspace.AuthorizationEpoch, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var membership = await memberships.GetActiveByUserAsync(tenantId, userId, cancellationToken);
        if (membership is null)
        {
            return null;
        }

        var resolved = new CachedAuthorizationContext(
            membership.Id, membership.UserId, membership.PrincipalType, membership.Role, membership.Tier);
        await authorizationCache.SetAsync(tenantId, userId, workspace.AuthorizationEpoch, resolved, cancellationToken);
        return resolved;
    }

    private static (string Issuer, string Subject, PrincipalType PrincipalType)? ResolveIdentity(
        ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var issuer = principal.FindFirstValue("iss")
            ?? principal.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Issuer;
        var principalType = string.Equals(
            principal.FindFirstValue("principal_type"),
            "service_account",
            StringComparison.OrdinalIgnoreCase)
            ? PrincipalType.ServiceAccount
            : PrincipalType.User;
        var subject = principalType == PrincipalType.ServiceAccount
            ? principal.FindFirstValue("client_id") ?? principal.FindFirstValue("azp") ?? principal.FindFirstValue("sub")
            : principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject)
            ? null
            : (issuer, subject, principalType);
    }

    private static Task UnauthorizedAsync(HttpContext context, string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            type: "/problems/invalid-membership",
            title: "Tenant membership is required.",
            detail: detail)
        .ExecuteAsync(context);

    private static bool RequiresTenant(HttpContext context)
    {
        var path = context.Request.Path;
        if (!path.StartsWithSegments("/api/v1")) return false;
        if (path.StartsWithSegments("/api/v1/choices")) return false;
        if (path.StartsWithSegments("/api/v1/bootstrap")) return false;
        if (path.StartsWithSegments("/api/v1/register")) return false;
        if (path.StartsWithSegments("/api/v1/auth")) return false;
        if (path.StartsWithSegments("/api/v1/me")) return false;
        // Only the literal "create workspace" endpoint is exempt - a prefix match here would also
        // swallow authenticated, tenant-scoped nested routes like POST /workspaces/current/settings/logo/presign.
        if (path.Equals("/api/v1/workspaces", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsPost(context.Request.Method))
        {
            return false;
        }

        return true;
    }

    private static bool IsPublicInvitation(PathString path) =>
        path.StartsWithSegments("/api/v1/workspaces")
        && (path.Value?.EndsWith("/invitations/accept", StringComparison.OrdinalIgnoreCase) == true
            || path.Value?.EndsWith("/invitations/accept-external", StringComparison.OrdinalIgnoreCase) == true);
}
