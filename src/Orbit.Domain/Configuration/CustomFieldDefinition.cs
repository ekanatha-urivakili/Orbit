using Orbit.Domain.Common;

namespace Orbit.Domain.Configuration;

public enum CustomFieldType
{
    Text,
    Number,
    Date,
    Checkbox
}

/// <summary>
/// A tenant-owned, administrator-created field definition. First slice of the configurability
/// engine (ORBIT-WORK-MANAGEMENT-ARCHITECTURE.md §13.5 step 6): definitions only - not yet wired
/// into work-item creation/storage, screens, or query projections, unlike the stable
/// <see cref="WorkItemTypeDefinition"/> registry it's modeled on. Select-style field types wait on
/// the companion choice-options subsystem the same architecture step calls out separately.
/// </summary>
public sealed class CustomFieldDefinition
{
    private CustomFieldDefinition()
    {
    }

    private CustomFieldDefinition(
        Guid id,
        Guid tenantId,
        string key,
        string label,
        CustomFieldType fieldType,
        bool required,
        int order,
        DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        Key = key;
        Label = label;
        FieldType = fieldType;
        Required = required;
        Order = order;
        Enabled = true;
        Version = 1;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public CustomFieldType FieldType { get; private set; }
    public bool Required { get; private set; }
    public int Order { get; private set; }
    public bool Enabled { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static string NormalizeKey(string key) => key.Trim().ToLowerInvariant();

    public static CustomFieldDefinition Create(
        Guid tenantId,
        string key,
        string label,
        CustomFieldType fieldType,
        bool required,
        int order,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException("Tenant id is required.");
        }

        var normalizedKey = NormalizeKey(key);
        ValidateKey(normalizedKey);
        ValidateLabel(label);
        ValidateOrder(order);

        return new CustomFieldDefinition(
            Guid.CreateVersion7(), tenantId, normalizedKey, label.Trim(), fieldType, required, order, now);
    }

    public void Update(string label, bool required, int order, bool enabled, DateTimeOffset now)
    {
        ValidateLabel(label);
        ValidateOrder(order);

        Label = label.Trim();
        Required = required;
        Order = order;
        Enabled = enabled;
        Version++;
        UpdatedAt = now;
    }

    private static void ValidateKey(string key)
    {
        if (key.Length is < 2 or > 64 || key.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new DomainException("Field key must contain 2 to 64 lowercase letters, digits, or hyphens.");
        }
    }

    private static void ValidateLabel(string label)
    {
        if (label.Trim().Length is < 2 or > 80)
        {
            throw new DomainException("Field label must contain 2 to 80 characters.");
        }
    }

    private static void ValidateOrder(int order)
    {
        if (order is < 0 or > 10_000)
        {
            throw new DomainException("Field order must be between 0 and 10,000.");
        }
    }
}
