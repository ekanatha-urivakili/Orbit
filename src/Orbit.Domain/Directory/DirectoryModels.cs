using Orbit.Domain.Common;

namespace Orbit.Domain.Directory;

public sealed class Team
{
    private Team()
    {
    }

    private Team(Guid id, Guid tenantId, string name, Guid createdByMembershipId, DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        CreatedByMembershipId = createdByMembershipId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid CreatedByMembershipId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Team Create(
        Guid tenantId,
        string name,
        Guid createdByMembershipId,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || createdByMembershipId == Guid.Empty)
        {
            throw new DomainException("Tenant and creator membership ids are required.");
        }

        return new Team(Guid.CreateVersion7(), tenantId, NormalizeName(name), createdByMembershipId, now);
    }

    public void Rename(string name, DateTimeOffset now)
    {
        var normalized = NormalizeName(name);
        if (Name == normalized)
        {
            return;
        }

        Name = normalized;
        UpdatedAt = now;
    }

    private static string NormalizeName(string name)
    {
        var normalized = name.Trim();
        if (normalized.Length is < 2 or > 120)
        {
            throw new DomainException("Team name must contain 2 to 120 characters.");
        }

        return normalized;
    }
}

public sealed class TeamMembership
{
    private TeamMembership()
    {
    }

    private TeamMembership(Guid id, Guid tenantId, Guid teamId, Guid membershipId, DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        TeamId = teamId;
        MembershipId = membershipId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid MembershipId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static TeamMembership Create(
        Guid tenantId,
        Guid teamId,
        Guid membershipId,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || teamId == Guid.Empty || membershipId == Guid.Empty)
        {
            throw new DomainException("Tenant, team, and membership ids are required.");
        }

        return new TeamMembership(Guid.CreateVersion7(), tenantId, teamId, membershipId, now);
    }
}

/// <summary>
/// A tenant-scoped, permission-granting group of members - distinct from <see cref="Team"/>, which
/// is a plain directory list with no authorization effect. A group can be assigned project roles
/// (see <c>ProjectGroupRoleAssignment</c> in <c>Orbit.Domain.Access</c>) alongside individual
/// <c>ProjectRoleAssignment</c>s.
/// </summary>
public sealed class DirectoryGroup
{
    private DirectoryGroup()
    {
    }

    private DirectoryGroup(Guid id, Guid tenantId, string name, Guid createdByMembershipId, DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        CreatedByMembershipId = createdByMembershipId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid CreatedByMembershipId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static DirectoryGroup Create(
        Guid tenantId,
        string name,
        Guid createdByMembershipId,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || createdByMembershipId == Guid.Empty)
        {
            throw new DomainException("Tenant and creator membership ids are required.");
        }

        return new DirectoryGroup(Guid.CreateVersion7(), tenantId, NormalizeName(name), createdByMembershipId, now);
    }

    public void Rename(string name, DateTimeOffset now)
    {
        var normalized = NormalizeName(name);
        if (Name == normalized)
        {
            return;
        }

        Name = normalized;
        UpdatedAt = now;
    }

    private static string NormalizeName(string name)
    {
        var normalized = name.Trim();
        if (normalized.Length is < 2 or > 120)
        {
            throw new DomainException("Group name must contain 2 to 120 characters.");
        }

        return normalized;
    }
}

public sealed class GroupMembership
{
    private GroupMembership()
    {
    }

    private GroupMembership(Guid id, Guid tenantId, Guid groupId, Guid membershipId, DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        GroupId = groupId;
        MembershipId = membershipId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid MembershipId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static GroupMembership Create(
        Guid tenantId,
        Guid groupId,
        Guid membershipId,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || groupId == Guid.Empty || membershipId == Guid.Empty)
        {
            throw new DomainException("Tenant, group, and membership ids are required.");
        }

        return new GroupMembership(Guid.CreateVersion7(), tenantId, groupId, membershipId, now);
    }
}
