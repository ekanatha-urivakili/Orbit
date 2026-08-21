using Orbit.Domain.Common;
using Orbit.Domain.Configuration;
using Orbit.Domain.WorkItems;

namespace Orbit.Domain.Tests;

public sealed class WorkItemCustomFieldValueTests
{
    private static CustomFieldDefinition CreateDefinition(
        CustomFieldType fieldType, IReadOnlyList<CustomFieldChoiceOptionInput>? choiceOptions = null) =>
        CustomFieldDefinition.Create(
            Guid.NewGuid(), Guid.NewGuid(), "field", "Field", fieldType, false, 0, choiceOptions ?? [], [],
            DateTimeOffset.UtcNow);

    [Fact]
    public void Create_TrimsAndStoresTextValue()
    {
        var definition = CreateDefinition(CustomFieldType.Text);

        var value = WorkItemCustomFieldValue.Create(
            Guid.NewGuid(), Guid.NewGuid(), definition, [" hello "], DateTimeOffset.UtcNow);

        Assert.Equal(["hello"], value.Values);
        Assert.Equal(definition.Id, value.CustomFieldDefinitionId);
    }

    [Fact]
    public void Create_RejectsMultipleValuesForSingleValueField()
    {
        var definition = CreateDefinition(CustomFieldType.Text);

        var action = () => WorkItemCustomFieldValue.Create(
            Guid.NewGuid(), Guid.NewGuid(), definition, ["a", "b"], DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Theory]
    [InlineData("42.5")]
    [InlineData("0")]
    public void Create_AcceptsValidNumber(string raw)
    {
        var definition = CreateDefinition(CustomFieldType.Number);

        var value = WorkItemCustomFieldValue.Create(
            Guid.NewGuid(), Guid.NewGuid(), definition, [raw], DateTimeOffset.UtcNow);

        Assert.Equal([raw], value.Values);
    }

    [Fact]
    public void Create_RejectsNonNumericValueForNumberField()
    {
        var definition = CreateDefinition(CustomFieldType.Number);

        var action = () => WorkItemCustomFieldValue.Create(
            Guid.NewGuid(), Guid.NewGuid(), definition, ["not-a-number"], DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Create_RejectsInvalidDate()
    {
        var definition = CreateDefinition(CustomFieldType.Date);

        var action = () => WorkItemCustomFieldValue.Create(
            Guid.NewGuid(), Guid.NewGuid(), definition, ["not-a-date"], DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Theory]
    [InlineData("maybe")]
    [InlineData("1")]
    public void Create_RejectsNonBooleanCheckboxValue(string raw)
    {
        var definition = CreateDefinition(CustomFieldType.Checkbox);

        var action = () => WorkItemCustomFieldValue.Create(
            Guid.NewGuid(), Guid.NewGuid(), definition, [raw], DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Create_AcceptsKnownSingleChoiceOptionId()
    {
        var definition = CreateDefinition(
            CustomFieldType.SingleChoice, [new CustomFieldChoiceOptionInput(null, "High"), new CustomFieldChoiceOptionInput(null, "Low")]);
        var optionId = definition.ChoiceOptions[0].Id;

        var value = WorkItemCustomFieldValue.Create(
            Guid.NewGuid(), Guid.NewGuid(), definition, [optionId.ToString()], DateTimeOffset.UtcNow);

        Assert.Equal([optionId.ToString()], value.Values);
    }

    [Fact]
    public void Create_RejectsUnknownChoiceOptionId()
    {
        var definition = CreateDefinition(
            CustomFieldType.SingleChoice, [new CustomFieldChoiceOptionInput(null, "High")]);

        var action = () => WorkItemCustomFieldValue.Create(
            Guid.NewGuid(), Guid.NewGuid(), definition, [Guid.NewGuid().ToString()], DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Create_MultiChoice_AcceptsSeveralKnownOptions()
    {
        var definition = CreateDefinition(
            CustomFieldType.MultiChoice,
            [new CustomFieldChoiceOptionInput(null, "Red"), new CustomFieldChoiceOptionInput(null, "Blue")]);
        var optionIds = definition.ChoiceOptions.Select(option => option.Id.ToString()).ToArray();

        var value = WorkItemCustomFieldValue.Create(
            Guid.NewGuid(), Guid.NewGuid(), definition, optionIds, DateTimeOffset.UtcNow);

        Assert.Equal(optionIds, value.Values);
    }

    [Fact]
    public void Create_MultiChoice_RejectsDuplicateSelection()
    {
        var definition = CreateDefinition(
            CustomFieldType.MultiChoice, [new CustomFieldChoiceOptionInput(null, "Red")]);
        var optionId = definition.ChoiceOptions[0].Id.ToString();

        var action = () => WorkItemCustomFieldValue.Create(
            Guid.NewGuid(), Guid.NewGuid(), definition, [optionId, optionId], DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void SetValues_ReplacesExistingValuesAndBumpsUpdatedAt()
    {
        var definition = CreateDefinition(CustomFieldType.Text);
        var value = WorkItemCustomFieldValue.Create(
            Guid.NewGuid(), Guid.NewGuid(), definition, ["first"], DateTimeOffset.UtcNow);
        var later = DateTimeOffset.UtcNow.AddMinutes(5);

        value.SetValues(definition, ["second"], later);

        Assert.Equal(["second"], value.Values);
        Assert.Equal(later, value.UpdatedAt);
    }
}
