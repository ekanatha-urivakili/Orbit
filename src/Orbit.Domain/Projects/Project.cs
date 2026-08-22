using Orbit.Domain.Common;

namespace Orbit.Domain.Projects;

public sealed class Project
{
    private Project()
    {
    }

    private Project(Guid id, Guid tenantId, string key, string name, DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Key = key;
        Name = name;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public long NextItemSequence { get; private set; } = 1;
    public long Version { get; private set; } = 1;

    /// <summary>
    /// Bumped whenever workflow status catalog or custom-field config changes, so a HybridCache
    /// entry keyed on it (OBSERVABILITY-CACHING-ARCHITECTURE.md §5.1 principle 3) is invalidated
    /// on the next read without an explicit cache-delete call. Mirrors Workspace.AuthorizationEpoch.
    /// </summary>
    public long ConfigEpoch { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Project Create(Guid tenantId, string key, string name, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException("Tenant id is required.");
        }

        var normalizedKey = key.Trim().ToUpperInvariant();
        if (normalizedKey.Length is < 2 or > 10 || normalizedKey.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new DomainException("Project key must contain 2 to 10 ASCII letters or digits.");
        }

        var normalizedName = name.Trim();
        if (normalizedName.Length is < 2 or > 120)
        {
            throw new DomainException("Project name must contain 2 to 120 characters.");
        }

        return new Project(Guid.CreateVersion7(), tenantId, normalizedKey, normalizedName, now);
    }

    public long AllocateItemSequence(DateTimeOffset now)
    {
        var allocated = NextItemSequence;
        NextItemSequence++;
        Version++;
        UpdatedAt = now;
        return allocated;
    }

    public void IncrementConfigEpoch()
    {
        ConfigEpoch++;
        Version++;
    }
}
