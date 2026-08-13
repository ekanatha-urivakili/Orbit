using Orbit.Domain.Choices;
using Orbit.Domain.Common;
using Orbit.Domain.WorkItems;

namespace Orbit.Domain.Tests;

public sealed class WorkItemTests
{
    [Fact]
    public void Create_AssignsStableKeyAndInitialChoices()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var item = WorkItem.Create(
            tenantId,
            projectId,
            42,
            "ORB",
            "Build the first board",
            null,
            WorkItemType.Story,
            Priority.High,
            now);

        Assert.Equal("ORB-42", item.Key);
        Assert.Equal(WorkItemStatus.Backlog, item.Status);
        Assert.Equal(1, item.Version);
    }

    [Fact]
    public void Create_RejectsInvalidSummary()
    {
        var action = () => WorkItem.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            "ORB",
            "x",
            null,
            WorkItemType.Task,
            Priority.Medium,
            DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void ChangeStatus_IncrementsVersionOnlyWhenStateChanges()
    {
        var item = WorkItem.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            "ORB",
            "Move this card",
            null,
            WorkItemType.Task,
            Priority.Medium,
            DateTimeOffset.UtcNow);

        item.ChangeStatus(WorkItemStatus.InProgress, DateTimeOffset.UtcNow.AddMinutes(1));
        item.ChangeStatus(WorkItemStatus.InProgress, DateTimeOffset.UtcNow.AddMinutes(2));

        Assert.Equal(WorkItemStatus.InProgress, item.Status);
        Assert.Equal(2, item.Version);
    }

    [Fact]
    public void Reorder_UpdatesRankAndIncrementsVersion()
    {
        var item = WorkItem.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            "ORB",
            "Reprioritize this card",
            null,
            WorkItemType.Task,
            Priority.Medium,
            DateTimeOffset.UtcNow);

        item.Reorder(512m, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(512m, item.Rank);
        Assert.Equal(2, item.Version);
    }

    [Fact]
    public void Reorder_IsNoOp_WhenRankUnchanged()
    {
        var item = WorkItem.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            "ORB",
            "Keep this card in place",
            null,
            WorkItemType.Task,
            Priority.Medium,
            DateTimeOffset.UtcNow);

        item.Reorder(item.Rank, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(1, item.Version);
    }

    [Fact]
    public void SetDetails_RequiresEpicName()
    {
        var item = WorkItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, "ORB", "Plan the release", null,
            WorkItemType.Epic, Priority.Medium, DateTimeOffset.UtcNow);

        var action = () => item.SetDetails(
            null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void SetDetails_NormalizesLabelsAndCountries()
    {
        var item = WorkItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, "ORB", "Investigate latency", null,
            WorkItemType.Spike, Priority.High, DateTimeOffset.UtcNow);

        item.SetDetails(
            null, null, null, null, null, null, null, null, null, 3,
            null, null, [" backend ", "Backend"], [" US "], ["trace.txt"]);

        Assert.Equal(["backend"], item.Labels, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(["US"], item.Countries);
        Assert.Equal(3, item.StoryPoints);
    }

    [Fact]
    public void Update_ChangesSummaryDescriptionPriorityAndBumpsVersion()
    {
        var now = DateTimeOffset.UtcNow;
        var item = WorkItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, "ORB", "Original summary", null,
            WorkItemType.Task, Priority.Medium, now);

        item.Update(
            "Updated summary", "Updated description", Priority.High,
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, now.AddMinutes(5));

        Assert.Equal("Updated summary", item.Summary);
        Assert.Equal("Updated description", item.Description);
        Assert.Equal(Priority.High, item.Priority);
        Assert.Equal(2, item.Version);
        Assert.Equal(now.AddMinutes(5), item.UpdatedAt);
    }

    [Fact]
    public void Update_RejectsTooShortSummary()
    {
        var now = DateTimeOffset.UtcNow;
        var item = WorkItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, "ORB", "Original summary", null,
            WorkItemType.Task, Priority.Medium, now);

        var action = () => item.Update(
            "x", null, Priority.Medium,
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Update_RequiresEpicNameForEpic()
    {
        var now = DateTimeOffset.UtcNow;
        var item = WorkItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, "ORB", "Plan the release", null,
            WorkItemType.Epic, Priority.Medium, now);

        var action = () => item.Update(
            "Plan the release", null, Priority.Medium,
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, now);

        Assert.Throws<DomainException>(action);
    }
}
