using Orbit.Domain.Common;
using Orbit.Domain.Configuration;

namespace Orbit.Domain.Tests;

public sealed class CustomFieldDefinitionTests
{
    [Fact]
    public void Create_NormalizesKeyAndTrimsLabel()
    {
        var tenantId = Guid.NewGuid();

        var definition = CustomFieldDefinition.Create(
            tenantId, " Story-Points-Target ", " Story points target ", CustomFieldType.Number, true, 10, DateTimeOffset.UtcNow);

        Assert.Equal("story-points-target", definition.Key);
        Assert.Equal("Story points target", definition.Label);
        Assert.Equal(CustomFieldType.Number, definition.FieldType);
        Assert.True(definition.Required);
        Assert.True(definition.Enabled);
        Assert.Equal(1, definition.Version);
    }

    [Fact]
    public void Create_RejectsInvalidKey()
    {
        var action = () => CustomFieldDefinition.Create(
            Guid.NewGuid(), "not a valid key!", "Label", CustomFieldType.Text, false, 0, DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Update_ChangesLabelAndBumpsVersionWithoutTouchingKeyOrFieldType()
    {
        var definition = CustomFieldDefinition.Create(
            Guid.NewGuid(), "severity", "Severity", CustomFieldType.Text, false, 0, DateTimeOffset.UtcNow);

        definition.Update("Severity level", true, 5, false, DateTimeOffset.UtcNow);

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
            Guid.NewGuid(), "severity", "Severity", CustomFieldType.Text, false, 0, DateTimeOffset.UtcNow);

        var action = () => definition.Update("Severity", false, 10_001, true, DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }
}
