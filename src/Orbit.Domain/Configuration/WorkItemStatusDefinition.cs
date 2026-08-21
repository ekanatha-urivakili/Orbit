using Orbit.Domain.Choices;
using Orbit.Domain.Common;

namespace Orbit.Domain.Configuration;

/// <summary>
/// A project-owned, administrator-editable workflow status (ORBIT-WORK-MANAGEMENT-ARCHITECTURE.md
/// §13.5 next-increment: "Edit workflow" / "Add status"). Replaces the previously fixed
/// <c>WorkItemStatus</c> enum as the source of truth for <see cref="WorkItems.WorkItem.StatusId"/>
/// and <see cref="Boards.BoardColumn.StatusId"/>: every project is seeded with the same six
/// defaults <see cref="CreateSoftwareDefaults"/> used to produce (so existing data keeps working
/// unchanged), and administrators can rename, recolor, reorder, recategorize, or add further
/// statuses from there. <see cref="Category"/> drives the handful of places that need to reason
/// about "is this status done/in-progress" generically (sprint completion, cycle-time reports)
/// without hard-coding a specific status. <see cref="IsDefault"/> is a separate, explicit flag for
/// which status a newly created work item gets — deliberately not derived from <see cref="Order"/>,
/// since reordering the workflow's display order must never silently change what a "new" item's
/// starting state is.
/// </summary>
public sealed class WorkItemStatusDefinition
{
    private WorkItemStatusDefinition()
    {
    }

    private WorkItemStatusDefinition(
        Guid id,
        Guid tenantId,
        Guid projectId,
        string key,
        string name,
        StatusCategory category,
        int order,
        string colorToken,
        bool isSystem,
        bool isDefault,
        DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        Key = key;
        Name = name;
        Category = category;
        Order = order;
        ColorToken = colorToken;
        IsSystem = isSystem;
        IsDefault = isDefault;
        Version = 1;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public StatusCategory Category { get; private set; }
    public int Order { get; private set; }
    public string ColorToken { get; private set; } = string.Empty;

    /// <summary>Seeded by <see cref="CreateSoftwareDefaults"/>; cannot be deleted, but can be renamed/reordered/recategorized like any other status.</summary>
    public bool IsSystem { get; private set; }

    /// <summary>The status a newly created work item in this project starts in. Exactly one status per project should carry this flag; enforced by the handler that flips it, not by this entity alone.</summary>
    public bool IsDefault { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static IReadOnlyList<WorkItemStatusDefinition> CreateSoftwareDefaults(
        Guid tenantId, Guid projectId, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty)
        {
            throw new DomainException("Tenant and project ids are required.");
        }

        (string Key, string Name, StatusCategory Category, string Color)[] defaults =
        [
            ("backlog", "Backlog", StatusCategory.ToDo, "slate"),
            ("selected", "Selected", StatusCategory.ToDo, "cyan"),
            ("in-progress", "In progress", StatusCategory.InProgress, "blue"),
            ("in-review", "In review", StatusCategory.InProgress, "amber"),
            ("done", "Done", StatusCategory.Done, "green"),
            ("blocked", "Blocked", StatusCategory.InProgress, "red"),
        ];

        return defaults
            .Select((seed, index) => new WorkItemStatusDefinition(
                Guid.CreateVersion7(), tenantId, projectId, seed.Key, seed.Name, seed.Category,
                (index + 1) * 10, seed.Color, isSystem: true, isDefault: index == 0, now))
            .ToArray();
    }

    public static WorkItemStatusDefinition Create(
        Guid tenantId,
        Guid projectId,
        string key,
        string name,
        StatusCategory category,
        int order,
        string colorToken,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty)
        {
            throw new DomainException("Tenant and project ids are required.");
        }

        var normalizedKey = NormalizeKey(key);
        ValidateKey(normalizedKey);
        var definition = new WorkItemStatusDefinition(
            Guid.CreateVersion7(), tenantId, projectId, normalizedKey, name, category, order,
            colorToken, isSystem: false, isDefault: false, now);
        definition.Update(name, category, order, colorToken, now);
        return definition;
    }

    public void Update(string name, StatusCategory category, int order, string colorToken, DateTimeOffset now)
    {
        var normalizedName = name.Trim();
        if (normalizedName.Length is < 1 or > 60)
        {
            throw new DomainException("Status name must contain 1 to 60 characters.");
        }

        if (!Enum.IsDefined(category))
        {
            throw new DomainException("Status category is invalid.");
        }

        if (order is < 0 or > 100_000)
        {
            throw new DomainException("Status order must be between 0 and 100,000.");
        }

        var normalizedColorToken = colorToken.Trim().ToLowerInvariant();
        if (normalizedColorToken.Length is < 2 or > 32
            || normalizedColorToken.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new DomainException("Status color token is invalid.");
        }

        Name = normalizedName;
        Category = category;
        Order = order;
        ColorToken = normalizedColorToken;
        Version++;
        UpdatedAt = now;
    }

    /// <summary>Flips this status's default flag. The caller (handler) is responsible for clearing the flag on whichever status previously held it, so exactly one status per project stays default.</summary>
    public void SetDefault(bool isDefault, DateTimeOffset now)
    {
        if (IsDefault == isDefault)
        {
            return;
        }

        IsDefault = isDefault;
        Version++;
        UpdatedAt = now;
    }

    public static string NormalizeKey(string key) => key.Trim().ToLowerInvariant();

    private static void ValidateKey(string key)
    {
        if (key.Length is < 1 or > 64 || key.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new DomainException("Status key must contain 1 to 64 lowercase letters, digits, or hyphens.");
        }
    }
}
