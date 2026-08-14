using Orbit.Domain.Common;

namespace Orbit.Domain.Access;

public enum WorkspaceInvitationStatus
{
    Active,
    Accepted,
    Revoked
}

public sealed class WorkspaceInvitation
{
    private WorkspaceInvitation()
    {
    }

    private WorkspaceInvitation(
        Guid id,
        Guid tenantId,
        string normalizedEmail,
        TenantRole role,
        Guid? teamId,
        string tokenHash,
        Guid invitedByMembershipId,
        DateTimeOffset now,
        DateTimeOffset expiresAt)
    {
        Id = id;
        TenantId = tenantId;
        NormalizedEmail = normalizedEmail;
        Role = role;
        TeamId = teamId;
        TokenHash = tokenHash;
        InvitedByMembershipId = invitedByMembershipId;
        Status = WorkspaceInvitationStatus.Active;
        CreatedAt = now;
        UpdatedAt = now;
        ExpiresAt = expiresAt;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string NormalizedEmail { get; private set; } = string.Empty;
    public TenantRole Role { get; private set; }
    public Guid? TeamId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public Guid InvitedByMembershipId { get; private set; }
    public WorkspaceInvitationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }
    public long Version { get; private set; }

    public static WorkspaceInvitation Create(
        Guid tenantId,
        string normalizedEmail,
        TenantRole role,
        Guid? teamId,
        string tokenHash,
        Guid invitedByMembershipId,
        DateTimeOffset now,
        TimeSpan lifetime)
    {
        Validate(tenantId, normalizedEmail, role, tokenHash, invitedByMembershipId, lifetime);
        return new WorkspaceInvitation(
            Guid.CreateVersion7(),
            tenantId,
            normalizedEmail,
            role,
            teamId,
            tokenHash,
            invitedByMembershipId,
            now,
            now + lifetime);
    }

    public bool IsUsable(DateTimeOffset now) =>
        Status == WorkspaceInvitationStatus.Active && ExpiresAt > now;

    public void Renew(
        TenantRole role,
        Guid? teamId,
        string tokenHash,
        Guid invitedByMembershipId,
        DateTimeOffset now,
        TimeSpan lifetime)
    {
        Validate(TenantId, NormalizedEmail, role, tokenHash, invitedByMembershipId, lifetime);
        Role = role;
        TeamId = teamId;
        TokenHash = tokenHash;
        InvitedByMembershipId = invitedByMembershipId;
        Status = WorkspaceInvitationStatus.Active;
        UpdatedAt = now;
        ExpiresAt = now + lifetime;
        AcceptedAt = null;
        AcceptedByUserId = null;
        Version++;
    }

    public void Accept(Guid userId, DateTimeOffset now)
    {
        if (userId == Guid.Empty || !IsUsable(now))
        {
            throw new DomainException("The invitation is invalid or has expired.");
        }

        Status = WorkspaceInvitationStatus.Accepted;
        AcceptedAt = now;
        AcceptedByUserId = userId;
        UpdatedAt = now;
        Version++;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (Status != WorkspaceInvitationStatus.Active)
        {
            return;
        }

        Status = WorkspaceInvitationStatus.Revoked;
        UpdatedAt = now;
        Version++;
    }

    private static void Validate(
        Guid tenantId,
        string normalizedEmail,
        TenantRole role,
        string tokenHash,
        Guid invitedByMembershipId,
        TimeSpan lifetime)
    {
        if (tenantId == Guid.Empty || invitedByMembershipId == Guid.Empty)
        {
            throw new DomainException("Tenant and inviter membership ids are required.");
        }

        if (normalizedEmail.Length is < 3 or > 320 || string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Length > 64)
        {
            throw new DomainException("Invitation identity data is invalid.");
        }

        if (role == TenantRole.Owner)
        {
            throw new DomainException("Workspace ownership cannot be granted by invitation.");
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new DomainException("Invitation lifetime must be positive.");
        }
    }
}
