using System.Globalization;
using System.Text;
using Orbit.Domain.Common;

namespace Orbit.Domain.Workspaces;

public sealed class Workspace
{
    private Workspace()
    {
    }

    private Workspace(Guid id, string slug, string name, DateTimeOffset createdAt)
    {
        Id = id;
        Slug = slug;
        Name = name;
        AuthorizationEpoch = 1;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public long AuthorizationEpoch { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Workspace Create(string name, DateTimeOffset now)
    {
        var normalizedName = name.Trim();
        if (normalizedName.Length is < 2 or > 120)
        {
            throw new DomainException("Workspace name must contain 2 to 120 characters.");
        }

        var slug = CreateSlug(normalizedName);
        if (slug.Length < 2)
        {
            throw new DomainException("Workspace name must contain at least two letters or digits.");
        }

        return new Workspace(Guid.CreateVersion7(), slug, normalizedName, now);
    }

    /// <summary>
    /// Bumped on every permission-affecting mutation (role change, membership deactivation,
    /// project/group role assignment, group membership change) so a cached authorization context
    /// keyed on this epoch becomes unreachable rather than needing explicit invalidation.
    /// </summary>
    public void IncrementAuthorizationEpoch() => AuthorizationEpoch++;

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
