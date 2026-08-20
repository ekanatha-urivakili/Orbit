using Orbit.Domain.Common;
using Orbit.Domain.Configuration;

namespace Orbit.Domain.Tests;

public sealed class CustomFieldDefinitionTests
{
    [Fact]
    public void Create_NormalizesKeyAndTrimsLabel()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var definition = CustomFieldDefinition.Create(
            tenantId, projectId, " Story-Points-Target ", " Story points target ", CustomFieldType.Number, true, 10,
            [], DateTimeOffset.UtcNow);

        Assert.Equal("story-points-target", definition.Key);
        Assert.Equal("Story points target", definition.Label);
        Assert.Equal(CustomFieldType.Number, definition.FieldType);
        Assert.Equal(projectId, definition.ProjectId);
        Assert.True(definition.Required);
        Assert.True(definition.Enabled);
        Assert.Equal(1, definition.Version);
    }

    [Fact]
    public void Create_RejectsInvalidKey()
    {
        var action = () => CustomFieldDefinition.Create(
            Guid.NewGuid(), Guid.NewGuid(), "not a valid key!", "Label", CustomFieldType.Text, false, 0, [],
            DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Update_ChangesLabelAndBumpsVersionWithoutTouchingKeyOrFieldType()
    {
        var definition = CustomFieldDefinition.Create(
            Guid.NewGuid(), Guid.NewGuid(), "severity", "Severity", CustomFieldType.Text, false, 0, [],
            DateTimeOffset.UtcNow);

        definition.Update("Severity level", true, 5, false, [], DateTimeOffset.UtcNow);

        Assert.Equal("severity", definition.Key);
        Assert.Equal(CustomFieldType.Text, definition.FieldType);
        Assert.Equal("Severity level", definition.Label);
        Assert.True(definition.Required);
        Assert.Equal(5, definition.Order);
        Assert.False(definition.Enabled);
        Assert.Equal(2, definition.Version);
    }

    [Fact]
    public void Update_RejectsOutOfRangeOrder()
    {
        var definition = CustomFieldDefinition.Create(
            Guid.NewGuid(), Guid.NewGuid(), "severity", "Severity", CustomFieldType.Text, false, 0, [],
            DateTimeOffset.UtcNow);

        var action = () => definition.Update("Severity", false, 10_001, true, [], DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Create_RejectsChoiceOptionsOnNonChoiceField()
    {
        var action = () => CustomFieldDefinition.Create(
            Guid.NewGuid(), Guid.NewGuid(), "severity", "Severity", CustomFieldType.Text, false, 0,
            [new CustomFieldChoiceOptionInput(null, "High")], DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Create_RequiresAtLeastOneChoiceOptionForSingleChoice()
    {
        var action = () => CustomFieldDefinition.Create(
            Guid.NewGuid(), Guid.NewGuid(), "severity", "Severity", CustomFieldType.SingleChoice, false, 0, [],
            DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Create_AssignsStableOrderedChoiceOptions()
    {
        var definition = CustomFieldDefinition.Create(
            Guid.NewGuid(), Guid.NewGuid(), "severity", "Severity", CustomFieldType.SingleChoice, false, 0,
            [new CustomFieldChoiceOptionInput(null, "Low"), new CustomFieldChoiceOptionInput(null, "High")],
            DateTimeOffset.UtcNow);

        Assert.Equal(2, definition.ChoiceOptions.Count);
        Assert.Equal(["Low", "High"], definition.ChoiceOptions.OrderBy(o => o.Order).Select(o => o.Label));
        Assert.Equal([0, 1], definition.ChoiceOptions.OrderBy(o => o.Order).Select(o => o.Order));
    }

    [Fact]
    public void Update_PreservesExistingChoiceOptionIdsAndReordersAndRemoves()
    {
        var definition = CustomFieldDefinition.Create(
            Guid.NewGuid(), Guid.NewGuid(), "severity", "Severity", CustomFieldType.MultiChoice, false, 0,
            [new CustomFieldChoiceOptionInput(null, "Low"), new CustomFieldChoiceOptionInput(null, "Medium"),
             new CustomFieldChoiceOptionInput(null, "High")],
            DateTimeOffset.UtcNow);
        var lowId = definition.ChoiceOptions.Single(o => o.Label == "Low").Id;
        var highId = definition.ChoiceOptions.Single(o => o.Label == "High").Id;

        definition.Update(
            "Severity",
            false,
            0,
            true,
            [new CustomFieldChoiceOptionInput(highId, "High"), new CustomFieldChoiceOptionInput(lowId, "Low"),
             new CustomFieldChoiceOptionInput(null, "Critical")],
            DateTimeOffset.UtcNow);

        Assert.Equal(3, definition.ChoiceOptions.Count);
        Assert.Equal(highId, definition.ChoiceOptions.Single(o => o.Order == 0).Id);
        Assert.Equal(lowId, definition.ChoiceOptions.Single(o => o.Order == 1).Id);
        Assert.Equal("Critical", definition.ChoiceOptions.Single(o => o.Order == 2).Label);
    }
}
