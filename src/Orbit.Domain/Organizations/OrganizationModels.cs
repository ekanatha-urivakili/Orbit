using System.Globalization;
using System.Text;
using Orbit.Domain.Common;

namespace Orbit.Domain.Organizations;

public enum OrganizationRole
{
    Owner,
    Administrator,
    Member
}

public sealed class Organization
{
    private Organization()
    {
    }

    private Organization(Guid id, string slug, string name, DateTimeOffset createdAt)
    {
        Id = id;
        Slug = slug;
        Name = name;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public static Organization Create(string name, DateTimeOffset now)
    {
        var normalizedName = name.Trim();
        if (normalizedName.Length is < 2 or > 120)
        {
            throw new DomainException("Organization name must contain 2 to 120 characters.");
        }

        var slug = CreateSlug(normalizedName);
        if (slug.Length < 2)
        {
            throw new DomainException("Organization name must contain at least two letters or digits.");
        }

        return new Organization(Guid.CreateVersion7(), slug, normalizedName, now);
    }

    private static string CreateSlug(string name)
    {
        var builder = new StringBuilder();
        var pendingSeparator = false;
        foreach (var character in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }

            if (builder.Length == 63)
            {
                break;
            }
        }

        return builder.ToString().TrimEnd('-');
    }
}

/// <summary>
/// Links a <see cref="Identity.UserAccount"/> to an <see cref="Organization"/> with an org-scoped
/// role, independent of that user's per-workspace <see cref="Access.TenantMembership"/> role(s).
/// Not tenant-scoped (no RLS query filter) - same "global, app-layer-enforced" posture as
/// <see cref="Identity.UserAccount"/> and <see cref="Workspaces.Workspace"/> itself.
/// </summary>
public sealed class OrganizationMembership
{
    private OrganizationMembership()
    {
    }

    private OrganizationMembership(
        Guid id,
        Guid organizationId,
        Guid userId,
        OrganizationRole role,
        DateTimeOffset createdAt)
    {
        Id = id;
        OrganizationId = organizationId;
        UserId = userId;
        Role = role;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public OrganizationRole Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static OrganizationMembership Create(
        Guid organizationId,
        Guid userId,
        OrganizationRole role,
        DateTimeOffset now)
    {
        if (organizationId == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainException("Organization and user ids are required.");
        }

        return new OrganizationMembership(Guid.CreateVersion7(), organizationId, userId, role, now);
    }
}
