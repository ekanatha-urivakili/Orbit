using Orbit.Domain.Choices;
using Orbit.Domain.Common;

namespace Orbit.Domain.Configuration;

public sealed class WorkItemTypeDefinition
{
    private WorkItemTypeDefinition()
    {
    }

    private WorkItemTypeDefinition(
        Guid tenantId,
        WorkItemType id,
        string label,
        string description,
        int order,
        string colorToken,
        bool enabled,
        DateTimeOffset now)
    {
        TenantId = tenantId;
        Id = id;
        Label = label;
        Description = description;
        Order = order;
        ColorToken = colorToken;
        Enabled = enabled;
        Version = 1;
        UpdatedAt = now;
    }

    public Guid TenantId { get; private set; }
    public WorkItemType Id { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public string ColorToken { get; private set; } = string.Empty;
    public bool Enabled { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static IReadOnlyList<WorkItemTypeDefinition> CreateSoftwareDefaults(
        Guid tenantId,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException("Tenant id is required.");
        }

        return SystemChoiceCatalog.WorkItemTypes
            .Select(choice => new WorkItemTypeDefinition(
                tenantId,
                choice.Value,
                choice.Label,
                choice.Description,
                choice.Order,
                choice.ColorToken,
                choice.Enabled,
                now))
            .ToArray();
    }

    public void Update(
        string label,
        string description,
        int order,
        string colorToken,
        bool enabled,
        DateTimeOffset now)
    {
        var normalizedLabel = label.Trim();
        var normalizedDescription = description.Trim();
        var normalizedColorToken = colorToken.Trim().ToLowerInvariant();
        if (normalizedLabel.Length is < 2 or > 80)
        {
            throw new DomainException("Item type label must contain 2 to 80 characters.");
        }

        if (normalizedDescription.Length > 500)
        {
            throw new DomainException("Item type description cannot exceed 500 characters.");
        }

        if (order is < 0 or > 10_000)
        {
            throw new DomainException("Item type order must be between 0 and 10,000.");
        }

        if (normalizedColorToken.Length is < 2 or > 32
            || normalizedColorToken.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new DomainException("Item type color token is invalid.");
        }

        Label = normalizedLabel;
        Description = normalizedDescription;
        Order = order;
        ColorToken = normalizedColorToken;
        Enabled = enabled;
        Version++;
        UpdatedAt = now;
    }
}
