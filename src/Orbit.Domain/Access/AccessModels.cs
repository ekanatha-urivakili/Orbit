using Orbit.Domain.Common;

namespace Orbit.Domain.Access;

public enum PrincipalType
{
    User,
    ServiceAccount
}

public enum TenantRole
{
    Owner,
    Administrator,
    Member
}

public enum ProjectRole
{
    Administrator,
    Member,
    Viewer
}

public enum ProjectPermission
{
    View,
    CreateWorkItem,
    TransitionWorkItem,
    Administer
}

public sealed class TenantMembership
{
    private TenantMembership()
    {
    }

    private TenantMembership(
        Guid id,
        Guid tenantId,
        Guid? userId,
        string? issuer,
        string? subject,
        PrincipalType principalType,
        TenantRole role,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        Issuer = issuer;
        Subject = subject;
        PrincipalType = principalType;
        Role = role;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public string? Issuer { get; private set; }
    public string? Subject { get; private set; }
    public PrincipalType PrincipalType { get; private set; }
    public TenantRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static TenantMembership Create(
        Guid tenantId,
        string issuer,
        string subject,
        PrincipalType principalType,
        TenantRole role,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException("Tenant id is required.");
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

        return new TenantMembership(
            Guid.CreateVersion7(),
            tenantId,
            null,
            normalizedIssuer,
            normalizedSubject,
            principalType,
            role,
            now);
    }

    public static TenantMembership CreateForUser(
        Guid tenantId,
        Guid userId,
        TenantRole role,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainException("Tenant and user ids are required.");
        }

        return new TenantMembership(
            Guid.CreateVersion7(),
            tenantId,
            userId,
            null,
            null,
            PrincipalType.User,
            role,
            now);
    }

    public void ChangeRole(TenantRole role)
    {
        if (!IsActive)
        {
            throw new DomainException("An inactive membership cannot change role.");
        }

        Role = role;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}

public sealed class ProjectRoleAssignment
{
    private ProjectRoleAssignment()
    {
    }

    private ProjectRoleAssignment(
        Guid id,
        Guid tenantId,
        Guid projectId,
        Guid membershipId,
        ProjectRole role,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        MembershipId = membershipId;
        Role = role;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid MembershipId { get; private set; }
    public ProjectRole Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static ProjectRoleAssignment Create(
        Guid tenantId,
        Guid projectId,
        Guid membershipId,
        ProjectRole role,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty || membershipId == Guid.Empty)
        {
            throw new DomainException("Tenant, project, and membership ids are required.");
        }

        return new ProjectRoleAssignment(
            Guid.CreateVersion7(),
            tenantId,
            projectId,
            membershipId,
            role,
            now);
    }

    public void ChangeRole(ProjectRole role)
    {
        Role = role;
    }
}

/// <summary>
/// Grants a <see cref="Domain.Directory.DirectoryGroup"/> - and transitively every membership in
/// it - a project role, alongside individual <see cref="ProjectRoleAssignment"/>s. Kept as a
/// separate table rather than a nullable alternative on <see cref="ProjectRoleAssignment"/> so the
/// existing individual-assignment invariants and queries are untouched.
/// </summary>
public sealed class ProjectGroupRoleAssignment
{
    private ProjectGroupRoleAssignment()
    {
    }

    private ProjectGroupRoleAssignment(
        Guid id,
        Guid tenantId,
        Guid projectId,
        Guid groupId,
        ProjectRole role,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        GroupId = groupId;
        Role = role;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid GroupId { get; private set; }
    public ProjectRole Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static ProjectGroupRoleAssignment Create(
        Guid tenantId,
        Guid projectId,
        Guid groupId,
        ProjectRole role,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty || groupId == Guid.Empty)
        {
            throw new DomainException("Tenant, project, and group ids are required.");
        }

        return new ProjectGroupRoleAssignment(
            Guid.CreateVersion7(),
            tenantId,
            projectId,
            groupId,
            role,
            now);
    }

    public void ChangeRole(ProjectRole role)
    {
        Role = role;
    }
}
