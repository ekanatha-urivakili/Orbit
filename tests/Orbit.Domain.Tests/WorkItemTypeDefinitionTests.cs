using Orbit.Domain.Choices;
using Orbit.Domain.Common;
using Orbit.Domain.Configuration;

namespace Orbit.Domain.Tests;

public sealed class WorkItemTypeDefinitionTests
{
    [Fact]
    public void CreateSoftwareDefaults_CreatesStableCatalogForTenant()
    {
        var tenantId = Guid.NewGuid();

        var definitions = WorkItemTypeDefinition.CreateSoftwareDefaults(tenantId, DateTimeOffset.UtcNow);

        Assert.Equal(10, definitions.Count);
        Assert.Equal("Task", definitions.Single(definition => definition.Id == WorkItemType.Task).Label);
        Assert.True(definitions.Single(definition => definition.Id == WorkItemType.Subtask).Enabled);
        Assert.All(definitions, definition => Assert.Equal(tenantId, definition.TenantId));
    }

    [Fact]
    public void Update_RenamesWithoutChangingStableId()
    {
        var definition = WorkItemTypeDefinition.CreateSoftwareDefaults(Guid.NewGuid(), DateTimeOffset.UtcNow)
            .Single(itemType => itemType.Id == WorkItemType.Story);

        definition.Update("User story", "Customer-visible value.", 45, "indigo", true, DateTimeOffset.UtcNow);

        Assert.Equal(WorkItemType.Story, definition.Id);
        Assert.Equal("User story", definition.Label);
        Assert.Equal(2, definition.Version);
    }

    [Fact]
    public void Update_RejectsInvalidColorToken()
    {
        var definition = WorkItemTypeDefinition.CreateSoftwareDefaults(Guid.NewGuid(), DateTimeOffset.UtcNow)
            .Single(itemType => itemType.Id == WorkItemType.Task);

        var action = () => definition.Update("Task", string.Empty, 10, "not valid!", true, DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }
}
