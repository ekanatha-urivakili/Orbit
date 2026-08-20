using Orbit.Domain.Common;

namespace Orbit.Domain.Configuration;

public enum CustomFieldType
{
    Text,
    Number,
    Date,
    SingleChoice,
    MultiChoice,
    Checkbox
}

public sealed record CustomFieldChoiceOptionInput(Guid? Id, string Label);

public sealed class CustomFieldChoiceOption
{
    private CustomFieldChoiceOption()
    {
    }

    internal CustomFieldChoiceOption(Guid id, string label, int order)
    {
        Id = id;
        Label = label;
        Order = order;
    }

    public Guid Id { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public int Order { get; private set; }
}

/// <summary>
/// A project-owned, administrator-created field definition (ORBIT-WORK-MANAGEMENT-ARCHITECTURE.md
/// §13.5 step 6). Defines the schema; <c>WorkItemCustomFieldValue</c> holds the per-work-item
/// values. Screens (which fields show for which work item type) and query projections are not
/// wired yet.
/// </summary>
public sealed class CustomFieldDefinition
{
    private readonly List<CustomFieldChoiceOption> _choiceOptions = [];

    private CustomFieldDefinition()
    {
    }

    private CustomFieldDefinition(
        Guid id,
        Guid tenantId,
        Guid projectId,
        string key,
        string label,
        CustomFieldType fieldType,
        bool required,
        int order,
        DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
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
    public Guid ProjectId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public CustomFieldType FieldType { get; private set; }
    public bool Required { get; private set; }
    public int Order { get; private set; }
    public bool Enabled { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<CustomFieldChoiceOption> ChoiceOptions => _choiceOptions;

    public static string NormalizeKey(string key) => key.Trim().ToLowerInvariant();

    public static CustomFieldDefinition Create(
        Guid tenantId,
        Guid projectId,
        string key,
        string label,
        CustomFieldType fieldType,
        bool required,
        int order,
        IReadOnlyList<CustomFieldChoiceOptionInput> choiceOptions,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty)
        {
            throw new DomainException("Tenant and project ids are required.");
        }

        var normalizedKey = NormalizeKey(key);
        ValidateKey(normalizedKey);
        ValidateLabel(label);
        ValidateOrder(order);

        var definition = new CustomFieldDefinition(
            Guid.CreateVersion7(), tenantId, projectId, normalizedKey, label.Trim(), fieldType, required, order, now);
        definition.ReplaceChoiceOptions(choiceOptions);
        return definition;
    }

    public void Update(
        string label,
        bool required,
        int order,
        bool enabled,
        IReadOnlyList<CustomFieldChoiceOptionInput> choiceOptions,
        DateTimeOffset now)
    {
        ValidateLabel(label);
        ValidateOrder(order);

        Label = label.Trim();
        Required = required;
        Order = order;
        Enabled = enabled;
        ReplaceChoiceOptions(choiceOptions);
        Version++;
        UpdatedAt = now;
    }

    private void ReplaceChoiceOptions(IReadOnlyList<CustomFieldChoiceOptionInput> options)
    {
        var isChoiceType = FieldType is CustomFieldType.SingleChoice or CustomFieldType.MultiChoice;
        if (!isChoiceType)
        {
            if (options.Count > 0)
            {
                throw new DomainException("Only single-choice or multi-choice fields may have choice options.");
            }

            _choiceOptions.Clear();
            return;
        }

        if (options.Count == 0)
        {
            throw new DomainException("A single-choice or multi-choice field needs at least one option.");
        }

        if (options.Count > 100)
        {
            throw new DomainException("A custom field cannot have more than 100 choice options.");
        }

        var existingIds = _choiceOptions.Select(option => option.Id).ToHashSet();
        var assignedIds = new HashSet<Guid>();
        var seenLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var replacement = new List<CustomFieldChoiceOption>();
        for (var index = 0; index < options.Count; index++)
        {
            var input = options[index];
            ValidateChoiceLabel(input.Label);
            var trimmedLabel = input.Label.Trim();
            if (!seenLabels.Add(trimmedLabel))
            {
                throw new DomainException($"Duplicate choice option '{trimmedLabel}' is not allowed.");
            }

            var id = input.Id is { } existingId && existingIds.Contains(existingId) && assignedIds.Add(existingId)
                ? existingId
                : Guid.CreateVersion7();
            assignedIds.Add(id);
            replacement.Add(new CustomFieldChoiceOption(id, trimmedLabel, index));
        }

        _choiceOptions.Clear();
        _choiceOptions.AddRange(replacement);
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

    private static void ValidateChoiceLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label) || label.Trim().Length is < 1 or > 80)
        {
            throw new DomainException("Choice option label must contain 1 to 80 characters.");
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
