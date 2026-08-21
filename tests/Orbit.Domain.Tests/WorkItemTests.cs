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
        var statusId = Guid.NewGuid();
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
            statusId, now);

        Assert.Equal("ORB-42", item.Key);
        Assert.Equal(statusId, item.StatusId);
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
            Guid.NewGuid(), DateTimeOffset.UtcNow);

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
            Guid.NewGuid(), DateTimeOffset.UtcNow);
        var inProgressStatusId = Guid.NewGuid();

        item.ChangeStatus(inProgressStatusId, DateTimeOffset.UtcNow.AddMinutes(1));
        item.ChangeStatus(inProgressStatusId, DateTimeOffset.UtcNow.AddMinutes(2));

        Assert.Equal(inProgressStatusId, item.StatusId);
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
            Guid.NewGuid(), DateTimeOffset.UtcNow);

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
            Guid.NewGuid(), DateTimeOffset.UtcNow);

        item.Reorder(item.Rank, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(1, item.Version);
    }

    [Fact]
    public void ChangeType_UpdatesTypeAndIncrementsVersion()
    {
        var item = WorkItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, "ORB", "Reclassify this card", null,
            WorkItemType.Task, Priority.Medium, Guid.NewGuid(), DateTimeOffset.UtcNow);

        item.ChangeType(WorkItemType.Bug, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(WorkItemType.Bug, item.Type);
        Assert.Equal(2, item.Version);
    }

    [Fact]
    public void ChangeType_IsNoOp_WhenTypeUnchanged()
    {
        var item = WorkItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, "ORB", "Keep this card's type", null,
            WorkItemType.Story, Priority.Medium, Guid.NewGuid(), DateTimeOffset.UtcNow);

        item.ChangeType(WorkItemType.Story, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(1, item.Version);
    }

    [Theory]
    [InlineData(WorkItemType.Initiative)]
    [InlineData(WorkItemType.Epic)]
    [InlineData(WorkItemType.Subtask)]
    public void ChangeType_RejectsStructuralTypesAsSource(WorkItemType sourceType)
    {
        var item = WorkItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, "ORB", "Structural item", null,
            sourceType, Priority.Medium, Guid.NewGuid(), DateTimeOffset.UtcNow);

        var action = () => item.ChangeType(WorkItemType.Task, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Throws<DomainException>(action);
    }

    [Theory]
    [InlineData(WorkItemType.Initiative)]
    [InlineData(WorkItemType.Epic)]
    [InlineData(WorkItemType.Subtask)]
    public void ChangeType_RejectsStructuralTypesAsTarget(WorkItemType targetType)
    {
        var item = WorkItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, "ORB", "Regular item", null,
            WorkItemType.Task, Priority.Medium, Guid.NewGuid(), DateTimeOffset.UtcNow);

        var action = () => item.ChangeType(targetType, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void SetFlagged_TogglesAndIncrementsVersion()
    {
        var item = WorkItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, "ORB", "Flag this card", null,
            WorkItemType.Task, Priority.Medium, Guid.NewGuid(), DateTimeOffset.UtcNow);

        item.SetFlagged(true, DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.True(item.IsFlagged);
        Assert.Equal(2, item.Version);

        item.SetFlagged(true, DateTimeOffset.UtcNow.AddMinutes(2));
        Assert.Equal(2, item.Version);

        item.SetFlagged(false, DateTimeOffset.UtcNow.AddMinutes(3));
        Assert.False(item.IsFlagged);
        Assert.Equal(3, item.Version);
    }

    [Fact]
    public void SetCover_UpdatesCoverAttachmentId()
    {
        var item = WorkItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, "ORB", "Cover this card", null,
            WorkItemType.Task, Priority.Medium, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var attachmentId = Guid.NewGuid();

        item.SetCover(attachmentId, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(attachmentId, item.CoverAttachmentId);
        Assert.Equal(2, item.Version);
    }

    [Fact]
    public void Archive_ThenUnarchive_RoundTrips()
    {
        var item = WorkItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, "ORB", "Archive this card", null,
            WorkItemType.Task, Priority.Medium, Guid.NewGuid(), DateTimeOffset.UtcNow);

        item.Archive(DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.True(item.IsArchived);
        Assert.NotNull(item.ArchivedAt);
        Assert.Equal(2, item.Version);

        item.Archive(DateTimeOffset.UtcNow.AddMinutes(2));
        Assert.Equal(2, item.Version);

        item.Unarchive(DateTimeOffset.UtcNow.AddMinutes(3));
        Assert.False(item.IsArchived);
        Assert.Null(item.ArchivedAt);
        Assert.Equal(3, item.Version);
    }

    [Fact]
    public void MoveToProject_ReassignsProjectAndKey()
    {
        var item = WorkItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, "ORB", "Move this card", null,
            WorkItemType.Task, Priority.Medium, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var targetProjectId = Guid.NewGuid();

        item.MoveToProject(targetProjectId, 7, "TGT", DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(targetProjectId, item.ProjectId);
        Assert.Equal("TGT-7", item.Key);
        Assert.Equal(2, item.Version);
    }

    [Fact]
    public void SetDetails_RequiresEpicName()
    {
        var item = WorkItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, "ORB", "Plan the release", null,
            WorkItemType.Epic, Priority.Medium, Guid.NewGuid(), DateTimeOffset.UtcNow);

        var action = () => item.SetDetails(
            null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void SetDetails_NormalizesLabelsAndCountries()
    {
        var item = WorkItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, "ORB", "Investigate latency", null,
            WorkItemType.Spike, Priority.High, Guid.NewGuid(), DateTimeOffset.UtcNow);

        item.SetDetails(
            null, null, null, null, null, null, null, null, null, null, null, null, 3,
            [" backend ", "Backend"], [" US "], ["trace.txt"]);

        Assert.Equal(["backend"], item.Labels, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(["US"], item.Countries);
        Assert.Equal(3, item.StoryPoints);
    }

    [Fact]
    public void SetDetails_SetsDueDate()
    {
        var item = WorkItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, "ORB", "Ship the release", null,
            WorkItemType.Task, Priority.Medium, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var startDate = new DateOnly(2026, 8, 20);
        var dueDate = new DateOnly(2026, 8, 27);

        item.SetDetails(
            null, null, null, null, null, null, null, null, null, startDate, dueDate, null,
            null, null, null, null);

        Assert.Equal(startDate, item.StartDate);
        Assert.Equal(dueDate, item.DueDate);
    }

    [Fact]
    public void SetDetails_RejectsDueDateBeforeStartDate()
    {
        var item = WorkItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, "ORB", "Ship the release", null,
            WorkItemType.Task, Priority.Medium, Guid.NewGuid(), DateTimeOffset.UtcNow);

        var action = () => item.SetDetails(
            null, null, null, null, null, null, null, null, null,
            new DateOnly(2026, 8, 20), new DateOnly(2026, 8, 10), null,
            null, null, null, null);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Update_ChangesSummaryDescriptionPriorityAndBumpsVersion()
    {
        var now = DateTimeOffset.UtcNow;
        var item = WorkItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, "ORB", "Original summary", null,
            WorkItemType.Task, Priority.Medium, Guid.NewGuid(), now);

        item.Update(
            "Updated summary", "Updated description", Priority.High,
            null, null, null, null, null, null, null, null, null, null, null, null,
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
            WorkItemType.Task, Priority.Medium, Guid.NewGuid(), now);

        var action = () => item.Update(
            "x", null, Priority.Medium,
            null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Update_RequiresEpicNameForEpic()
    {
        var now = DateTimeOffset.UtcNow;
        var item = WorkItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, "ORB", "Plan the release", null,
            WorkItemType.Epic, Priority.Medium, Guid.NewGuid(), now);

        var action = () => item.Update(
            "Plan the release", null, Priority.Medium,
            null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, now);

        Assert.Throws<DomainException>(action);
    }
}
