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

/// <summary>
/// A guest never gets the tenant-wide project-access shortcut regardless of
/// <see cref="TenantMembership.Role"/> - they see only projects with an explicit
/// <see cref="ProjectRoleAssignment"/> or <see cref="ProjectGroupRoleAssignment"/>. Kept orthogonal
/// to <see cref="TenantRole"/> (rather than a 4th role value) so existing role-based checks that
/// don't care about guest status don't need to change.
/// </summary>
public enum MembershipTier
{
    Standard,
    Guest
}

public enum ProjectPermission
{
    View,
    CreateWorkItem,
    TransitionWorkItem,
    Administer
}

/// <summary>
/// A tenant-defined, named set of <see cref="ProjectPermission"/>s that can be granted to memberships
/// or directory groups on a project via <see cref="ProjectRoleAssignment"/>/<see cref="ProjectGroupRoleAssignment"/>.
/// Every tenant is seeded with three <see cref="IsSystem"/> roles (Administrator/Member/Viewer,
/// reproducing the permission sets the old <c>ProjectRole</c> enum hardcoded) via <see cref="SeedSystemRoles"/>;
/// system roles cannot be renamed or deleted but their permissions can still be edited, since the point of
/// this schema is fully user-defined permission sets, not just user-defined names.
/// </summary>
public sealed class Role
{
    private readonly List<RolePermission> _permissions = [];

    private Role()
    {
    }

    private Role(Guid id, Guid tenantId, string name, bool isSystem, DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        IsSystem = isSystem;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsSystem { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyCollection<RolePermission> Permissions => _permissions;

    public static Role Create(
        Guid tenantId,
        string name,
        bool isSystem,
        IEnumerable<ProjectPermission> permissions,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException("Tenant id is required.");
        }

        var role = new Role(Guid.CreateVersion7(), tenantId, NormalizeName(name), isSystem, now);
        foreach (var permission in permissions.Distinct())
        {
            role._permissions.Add(new RolePermission(role.Id, permission));
        }

        return role;
    }

    public static IReadOnlyList<Role> SeedSystemRoles(Guid tenantId, DateTimeOffset now) =>
    [
        Create(tenantId, "Administrator", isSystem: true,
            [ProjectPermission.View, ProjectPermission.CreateWorkItem, ProjectPermission.TransitionWorkItem, ProjectPermission.Administer],
            now),
        Create(tenantId, "Member", isSystem: true,
            [ProjectPermission.View, ProjectPermission.CreateWorkItem, ProjectPermission.TransitionWorkItem],
            now),
        Create(tenantId, "Viewer", isSystem: true, [ProjectPermission.View], now),
    ];

    public void Rename(string name)
    {
        if (IsSystem)
        {
            throw new DomainException("A system role cannot be renamed.");
        }

        Name = NormalizeName(name);
    }

    public void GrantPermission(ProjectPermission permission)
    {
        if (!_permissions.Any(p => p.Permission == permission))
        {
            _permissions.Add(new RolePermission(Id, permission));
        }
    }

    public void RevokePermission(ProjectPermission permission) =>
        _permissions.RemoveAll(p => p.Permission == permission);

    private static string NormalizeName(string name)
    {
        var normalized = name.Trim();
        if (normalized.Length is < 1 or > 100)
        {
            throw new DomainException("Role name must contain 1 to 100 characters.");
        }

        return normalized;
    }
}

/// <summary>
/// A single (<see cref="RoleId"/>, <see cref="Permission"/>) grant row backing <see cref="Role.Permissions"/>.
/// Plain data holder, not an aggregate of its own - created/removed only through <see cref="Role"/>.
/// </summary>
public sealed class RolePermission
{
    private RolePermission()
    {
    }

    internal RolePermission(Guid roleId, ProjectPermission permission)
    {
        RoleId = roleId;
        Permission = permission;
    }

    public Guid RoleId { get; private set; }
    public ProjectPermission Permission { get; private set; }
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
        MembershipTier tier,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        Issuer = issuer;
        Subject = subject;
        PrincipalType = principalType;
        Role = role;
        Tier = tier;
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
    public MembershipTier Tier { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static TenantMembership Create(
        Guid tenantId,
        string issuer,
        string subject,
        PrincipalType principalType,
        TenantRole role,
        DateTimeOffset now,
        MembershipTier tier = MembershipTier.Standard)
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

        ValidateTier(role, tier);
        return new TenantMembership(
            Guid.CreateVersion7(),
            tenantId,
            null,
            normalizedIssuer,
            normalizedSubject,
            principalType,
            role,
            tier,
            now);
    }

    public static TenantMembership CreateForUser(
        Guid tenantId,
        Guid userId,
        TenantRole role,
        DateTimeOffset now,
        MembershipTier tier = MembershipTier.Standard)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainException("Tenant and user ids are required.");
        }

        ValidateTier(role, tier);
        return new TenantMembership(
            Guid.CreateVersion7(),
            tenantId,
            userId,
            null,
            null,
            PrincipalType.User,
            role,
            tier,
            now);
    }

    public void ChangeRole(TenantRole role)
    {
        if (!IsActive)
        {
            throw new DomainException("An inactive membership cannot change role.");
        }

        ValidateTier(role, Tier);
        Role = role;
    }

    /// <summary>A guest can only ever hold the baseline Member role - promoting one to
    /// Owner/Administrator would hand out tenant-wide access, defeating the point of the tier.</summary>
    public void ChangeTier(MembershipTier tier)
    {
        if (!IsActive)
        {
            throw new DomainException("An inactive membership cannot change tier.");
        }

        ValidateTier(Role, tier);
        Tier = tier;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Reactivate(TenantRole role)
    {
        ValidateTier(role, Tier);
        Role = role;
        IsActive = true;
    }

    private static void ValidateTier(TenantRole role, MembershipTier tier)
    {
        if (tier == MembershipTier.Guest && role != TenantRole.Member)
        {
            throw new DomainException("A guest membership can only hold the Member role.");
        }
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
        Guid roleId,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        MembershipId = membershipId;
        RoleId = roleId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid MembershipId { get; private set; }
    public Guid RoleId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static ProjectRoleAssignment Create(
        Guid tenantId,
        Guid projectId,
        Guid membershipId,
        Guid roleId,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty || membershipId == Guid.Empty || roleId == Guid.Empty)
        {
            throw new DomainException("Tenant, project, membership, and role ids are required.");
        }

        return new ProjectRoleAssignment(
            Guid.CreateVersion7(),
            tenantId,
            projectId,
            membershipId,
            roleId,
            now);
    }

    public void ChangeRole(Guid roleId)
    {
        if (roleId == Guid.Empty)
        {
            throw new DomainException("Role id is required.");
        }

        RoleId = roleId;
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
        Guid roleId,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        GroupId = groupId;
        RoleId = roleId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid RoleId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static ProjectGroupRoleAssignment Create(
        Guid tenantId,
        Guid projectId,
        Guid groupId,
        Guid roleId,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty || groupId == Guid.Empty || roleId == Guid.Empty)
        {
            throw new DomainException("Tenant, project, group, and role ids are required.");
        }

        return new ProjectGroupRoleAssignment(
            Guid.CreateVersion7(),
            tenantId,
            projectId,
            groupId,
            roleId,
            now);
    }

    public void ChangeRole(Guid roleId)
    {
        if (roleId == Guid.Empty)
        {
            throw new DomainException("Role id is required.");
        }

        RoleId = roleId;
    }
}
