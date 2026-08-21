using Orbit.Domain.Choices;
using Orbit.Domain.Common;
using Orbit.Domain.Configuration;

namespace Orbit.Domain.Tests;

public sealed class WorkItemStatusDefinitionTests
{
    [Fact]
    public void CreateSoftwareDefaults_SeedsSixSystemStatusesInOrder()
    {
        var defaults = WorkItemStatusDefinition.CreateSoftwareDefaults(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal(6, defaults.Count);
        Assert.All(defaults, status => Assert.True(status.IsSystem));
        Assert.Equal(["backlog", "selected", "in-progress", "in-review", "done", "blocked"], defaults.Select(status => status.Key));
        Assert.Equal(defaults.OrderBy(status => status.Order), defaults);
    }

    [Fact]
    public void Create_NormalizesKeyAndIsNotSystem()
    {
        var status = WorkItemStatusDefinition.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Ready-For-QA", "Ready for QA", StatusCategory.InProgress, 45, "purple", DateTimeOffset.UtcNow);

        Assert.Equal("ready-for-qa", status.Key);
        Assert.Equal("Ready for QA", status.Name);
        Assert.False(status.IsSystem);
        // Create() applies Update() once internally to reuse its validation, so a freshly created status is Version 2.
        Assert.Equal(2, status.Version);
    }

    [Theory]
    [InlineData("Ready for QA")]
    [InlineData("ready_for_qa")]
    [InlineData("")]
    public void Create_RejectsInvalidKey(string key)
    {
        var action = () => WorkItemStatusDefinition.Create(
            Guid.NewGuid(), Guid.NewGuid(), key, "Name", StatusCategory.ToDo, 0, "slate", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Create_RejectsEmptyIdentifiers()
    {
        var action = () => WorkItemStatusDefinition.Create(
            Guid.Empty, Guid.NewGuid(), "ready", "Ready", StatusCategory.ToDo, 0, "slate", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Update_ChangesNameCategoryOrderAndColor_AndBumpsVersion()
    {
        var now = DateTimeOffset.UtcNow;
        var status = WorkItemStatusDefinition.Create(
            Guid.NewGuid(), Guid.NewGuid(), "backlog", "Backlog", StatusCategory.ToDo, 10, "slate", now);

        status.Update("Icebox", StatusCategory.Done, 99, "red", now.AddMinutes(1));

        Assert.Equal("Icebox", status.Name);
        Assert.Equal(StatusCategory.Done, status.Category);
        Assert.Equal(99, status.Order);
        Assert.Equal("red", status.ColorToken);
        Assert.Equal(3, status.Version);
    }

    [Fact]
    public void Update_RejectsTooLongName()
    {
        var status = WorkItemStatusDefinition.Create(
            Guid.NewGuid(), Guid.NewGuid(), "backlog", "Backlog", StatusCategory.ToDo, 10, "slate", DateTimeOffset.UtcNow);

        var action = () => status.Update(new string('a', 61), StatusCategory.ToDo, 10, "slate", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Update_RejectsInvalidColorToken()
    {
        var status = WorkItemStatusDefinition.Create(
            Guid.NewGuid(), Guid.NewGuid(), "backlog", "Backlog", StatusCategory.ToDo, 10, "slate", DateTimeOffset.UtcNow);

        var action = () => status.Update("Backlog", StatusCategory.ToDo, 10, "not a color!", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }
}
