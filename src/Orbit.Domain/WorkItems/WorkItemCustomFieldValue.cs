using Orbit.Domain.Common;
using Orbit.Domain.Configuration;

namespace Orbit.Domain.WorkItems;

/// <summary>
/// The value(s) a work item holds for one project-scoped <see cref="CustomFieldDefinition"/>.
/// One row per (work item, definition); <see cref="Values"/> holds zero or one entry for every
/// field type except <see cref="CustomFieldType.MultiChoice"/>, which may hold several selected
/// choice-option ids. Absence of a row means "no value set" - required-field enforcement is left
/// to the caller, not this entity, since retrofitting a field as required must not retroactively
/// break work items created before it existed.
/// </summary>
public sealed class WorkItemCustomFieldValue
{
    private WorkItemCustomFieldValue()
    {
    }

    private WorkItemCustomFieldValue(
        Guid id, Guid tenantId, Guid workItemId, Guid customFieldDefinitionId, DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        WorkItemId = workItemId;
        CustomFieldDefinitionId = customFieldDefinitionId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid WorkItemId { get; private set; }
    public Guid CustomFieldDefinitionId { get; private set; }
    public string[] Values { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static WorkItemCustomFieldValue Create(
        Guid tenantId,
        Guid workItemId,
        CustomFieldDefinition definition,
        IReadOnlyList<string> rawValues,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || workItemId == Guid.Empty)
        {
            throw new DomainException("Tenant and work item ids are required.");
        }

        var value = new WorkItemCustomFieldValue(Guid.CreateVersion7(), tenantId, workItemId, definition.Id, now);
        value.Values = Validate(definition, rawValues);
        return value;
    }

    public void SetValues(CustomFieldDefinition definition, IReadOnlyList<string> rawValues, DateTimeOffset now)
    {
        Values = Validate(definition, rawValues);
        UpdatedAt = now;
    }

    private static string[] Validate(CustomFieldDefinition definition, IReadOnlyList<string> rawValues)
    {
        var trimmed = rawValues
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();

        var isMultiValue = definition.FieldType == CustomFieldType.MultiChoice;
        if (!isMultiValue && trimmed.Length > 1)
        {
            throw new DomainException($"{definition.Label} accepts at most one value.");
        }

        switch (definition.FieldType)
        {
            case CustomFieldType.Number:
                if (trimmed.Any(value => !decimal.TryParse(value, out _)))
                {
                    throw new DomainException($"{definition.Label} must be a number.");
                }

                break;
            case CustomFieldType.Date:
                if (trimmed.Any(value => !DateOnly.TryParse(value, out _)))
                {
                    throw new DomainException($"{definition.Label} must be a valid date.");
                }

                break;
            case CustomFieldType.Checkbox:
                if (trimmed.Any(value => value is not ("true" or "false")))
                {
                    throw new DomainException($"{definition.Label} must be true or false.");
                }

                break;
            case CustomFieldType.SingleChoice:
            case CustomFieldType.MultiChoice:
                if (trimmed.Distinct(StringComparer.OrdinalIgnoreCase).Count() != trimmed.Length)
                {
                    throw new DomainException($"{definition.Label} cannot select the same option twice.");
                }

                var validOptionIds = definition.ChoiceOptions
                    .Select(option => option.Id.ToString())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (trimmed.Any(value => !validOptionIds.Contains(value)))
                {
                    throw new DomainException($"{definition.Label} was given an unknown choice option.");
                }

                break;
            case CustomFieldType.Text:
                if (trimmed.Any(value => value.Length > 2_000))
                {
                    throw new DomainException($"{definition.Label} cannot exceed 2,000 characters.");
                }

                break;
        }

        return trimmed;
    }
}
