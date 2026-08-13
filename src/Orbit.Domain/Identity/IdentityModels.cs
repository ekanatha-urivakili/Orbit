using System.Globalization;
using System.Text;
using Orbit.Domain.Common;

namespace Orbit.Domain.Identity;

public enum UserAccountStatus
{
    Active,
    Disabled
}

public enum SiteRole
{
    SuperAdministrator
}

public sealed class UserAccount
{
    private UserAccount()
    {
    }

    private UserAccount(
        Guid id,
        string normalizedEmail,
        string displayName,
        DateTimeOffset createdAt)
    {
        Id = id;
        NormalizedEmail = normalizedEmail;
        DisplayName = displayName;
        Version = 1;
        Status = UserAccountStatus.Active;
        EmailVerifiedAt = createdAt;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? AvatarUrl { get; private set; }
    public long Version { get; private set; }
    public UserAccountStatus Status { get; private set; }
    public DateTimeOffset? EmailVerifiedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static UserAccount Create(string email, string displayName, DateTimeOffset now)
    {
        var normalizedName = displayName.Trim();
        if (normalizedName.Length is < 2 or > 120)
        {
            throw new DomainException("Display name must contain 2 to 120 characters.");
        }

        return new UserAccount(Guid.CreateVersion7(), NormalizeEmail(email), normalizedName, now);
    }

    public void UpdateProfile(string displayName, string? avatarUrl, DateTimeOffset now)
    {
        var normalizedName = displayName.Trim();
        if (normalizedName.Length is < 2 or > 120)
        {
            throw new DomainException("Display name must contain 2 to 120 characters.");
        }

        var normalizedAvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim();
        if (normalizedAvatarUrl?.Length > 2048
            || normalizedAvatarUrl is not null
                && (!Uri.TryCreate(normalizedAvatarUrl, UriKind.Absolute, out var uri)
                    || uri.Scheme is not ("https" or "http")))
        {
            throw new DomainException("Avatar URL must be an absolute HTTP or HTTPS URL.");
        }

        if (DisplayName == normalizedName && AvatarUrl == normalizedAvatarUrl)
        {
            return;
        }

        DisplayName = normalizedName;
        AvatarUrl = normalizedAvatarUrl;
        Version++;
        UpdatedAt = now;
    }

    public static string NormalizeEmail(string email)
    {
        var normalized = email.Trim().Normalize(NormalizationForm.FormKC);
        var separator = normalized.LastIndexOf('@');
        if (separator < 1 || separator == normalized.Length - 1)
        {
            throw new DomainException("A valid email address is required.");
        }

        var localPart = normalized[..separator];
        var domain = normalized[(separator + 1)..];
        string asciiDomain;
        try
        {
            asciiDomain = new IdnMapping().GetAscii(domain);
        }
        catch (ArgumentException exception)
        {
            throw new DomainException("A valid email address is required.", exception);
        }

        var result = $"{localPart.ToLowerInvariant()}@{asciiDomain.ToLowerInvariant()}";
        if (result.Length > 320 || localPart.Length > 64 || asciiDomain.Length > 255)
        {
            throw new DomainException("Email address is too long.");
        }

        return result;
    }
}

/// <summary>
/// Links an external OIDC identity (issuer/subject pair) to a global <see cref="UserAccount"/>,
/// so a user can authenticate via that IdP and resolve to the same account's tenant memberships as
/// local login. Distinct from a tenant-scoped federated <c>TenantMembership</c> (issuer/subject),
/// which is used for members that have no local account at all (see
/// <c>CreateTenantMembershipCommand</c>) - both mechanisms coexist.
/// </summary>
public sealed class ExternalIdentity
{
    private ExternalIdentity()
    {
    }

    private ExternalIdentity(Guid id, Guid userId, string issuer, string subject, DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        Issuer = issuer;
        Subject = subject;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Issuer { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public static ExternalIdentity Create(Guid userId, string issuer, string subject, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("User id is required.");
        }

        var normalizedIssuer = issuer.Trim();
        var normalizedSubject = subject.Trim();
        if (normalizedIssuer.Length is < 1 or > 512)
        {
            throw new DomainException("Identity issuer must contain 1 to 512 characters.");
        }

        if (normalizedSubject.Length is < 1 or > 255)
        {
            throw new DomainException("Identity subject must contain 1 to 255 characters.");
        }

        return new ExternalIdentity(Guid.CreateVersion7(), userId, normalizedIssuer, normalizedSubject, now);
    }
}

public sealed class LocalCredential
{
    private LocalCredential()
    {
    }

    private LocalCredential(
        Guid userId,
        string passwordHash,
        string hashAlgorithm,
        int hashParametersVersion,
        DateTimeOffset changedAt)
    {
        UserId = userId;
        PasswordHash = passwordHash;
        HashAlgorithm = hashAlgorithm;
        HashParametersVersion = hashParametersVersion;
        ChangedAt = changedAt;
    }

    public Guid UserId { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;
    public string HashAlgorithm { get; private set; } = string.Empty;
    public int HashParametersVersion { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }

    public static LocalCredential Create(
        Guid userId,
        string passwordHash,
        string hashAlgorithm,
        int hashParametersVersion,
        DateTimeOffset now)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("User id and password hash are required.");
        }

        if (hashAlgorithm.Length is < 1 or > 32 || hashParametersVersion < 1)
        {
            throw new DomainException("Password hash metadata is invalid.");
        }

        return new LocalCredential(userId, passwordHash, hashAlgorithm, hashParametersVersion, now);
    }
}

public enum RefreshSessionStatus
{
    Active,
    Rotated,
    Revoked
}

/// <summary>
/// A rotating refresh-session record for one browser/device login. Sessions form a family
/// (<see cref="FamilyId"/>) linked by rotation; reuse of an already-rotated or revoked token is
/// treated as token theft and the whole family is revoked (see <see cref="Revoke"/> callers in
/// the refresh handler).
/// </summary>
public sealed class RefreshSession
{
    private RefreshSession()
    {
    }

    private RefreshSession(
        Guid id,
        Guid userId,
        Guid tenantId,
        Guid familyId,
        string tokenHash,
        string? userAgent,
        string? ipAddress,
        DateTimeOffset now,
        DateTimeOffset expiresAt)
    {
        Id = id;
        UserId = userId;
        TenantId = tenantId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        UserAgent = userAgent;
        IpAddress = ipAddress;
        Status = RefreshSessionStatus.Active;
        CreatedAt = now;
        LastUsedAt = now;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public string? UserAgent { get; private set; }
    public string? IpAddress { get; private set; }
    public RefreshSessionStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastUsedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedBySessionId { get; private set; }
    public long Version { get; private set; } = 1;

    public static RefreshSession CreateInitial(
        Guid userId,
        Guid tenantId,
        string tokenHash,
        string? userAgent,
        string? ipAddress,
        DateTimeOffset now,
        TimeSpan lifetime) =>
        Create(userId, tenantId, Guid.CreateVersion7(), tokenHash, userAgent, ipAddress, now, lifetime);

    /// <summary>
    /// Builds the next session in the rotation family. <paramref name="tenantId"/> is accepted
    /// explicitly (rather than reused from <see cref="TenantId"/>) so a refresh can switch the
    /// active workspace without starting a new session family.
    /// </summary>
    public RefreshSession CreateRotated(
        Guid tenantId,
        string tokenHash,
        string? userAgent,
        string? ipAddress,
        DateTimeOffset now,
        TimeSpan lifetime) =>
        Create(UserId, tenantId, FamilyId, tokenHash, userAgent, ipAddress, now, lifetime);

    private static RefreshSession Create(
        Guid userId,
        Guid tenantId,
        Guid familyId,
        string tokenHash,
        string? userAgent,
        string? ipAddress,
        DateTimeOffset now,
        TimeSpan lifetime)
    {
        if (userId == Guid.Empty || tenantId == Guid.Empty)
        {
            throw new DomainException("User id and tenant id are required.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("A token hash is required.");
        }

        return new RefreshSession(
            Guid.CreateVersion7(),
            userId,
            tenantId,
            familyId,
            tokenHash,
            Truncate(userAgent, 512),
            Truncate(ipAddress, 64),
            now,
            now + lifetime);
    }

    public bool IsUsable(DateTimeOffset now) =>
        Status == RefreshSessionStatus.Active && ExpiresAt > now;

    public void MarkRotated(Guid replacedBySessionId, DateTimeOffset now)
    {
        if (Status != RefreshSessionStatus.Active)
        {
            throw new DomainException("Only an active session can be rotated.");
        }

        Status = RefreshSessionStatus.Rotated;
        RevokedAt = now;
        ReplacedBySessionId = replacedBySessionId;
        Version++;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (Status != RefreshSessionStatus.Active)
        {
            return;
        }

        Status = RefreshSessionStatus.Revoked;
        RevokedAt = now;
        Version++;
    }

    public void Touch(DateTimeOffset now) => LastUsedAt = now;

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}

public sealed class SiteRoleAssignment
{
    private SiteRoleAssignment()
    {
    }

    private SiteRoleAssignment(Guid userId, SiteRole role, DateTimeOffset grantedAt)
    {
        UserId = userId;
        Role = role;
        GrantedAt = grantedAt;
    }

    public Guid UserId { get; private set; }
    public SiteRole Role { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }

    public static SiteRoleAssignment CreateSuperAdministrator(Guid userId, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("User id is required.");
        }

        return new SiteRoleAssignment(userId, SiteRole.SuperAdministrator, now);
    }
}
