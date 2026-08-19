using Orbit.Domain.Common;
using Orbit.Domain.WorkItems;

namespace Orbit.Domain.Tests;

public sealed class WorkItemHistoryEntryTests
{
    [Fact]
    public void Create_AssignsIdAndFields()
    {
        var tenantId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();
        var changedByMembershipId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var entry = WorkItemHistoryEntry.Create(
            tenantId, workItemId, changedByMembershipId, "Status", "Backlog", "InProgress", now);

        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal(tenantId, entry.TenantId);
        Assert.Equal(workItemId, entry.WorkItemId);
        Assert.Equal(changedByMembershipId, entry.ChangedByMembershipId);
        Assert.Equal("Status", entry.FieldName);
        Assert.Equal("Backlog", entry.OldValue);
        Assert.Equal("InProgress", entry.NewValue);
        Assert.Equal(now, entry.ChangedAt);
    }

    [Fact]
    public void Create_AllowsNullOldAndNewValues()
    {
        var entry = WorkItemHistoryEntry.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Ticket", null, "Created", DateTimeOffset.UtcNow);

        Assert.Null(entry.OldValue);
        Assert.Equal("Created", entry.NewValue);
    }

    [Fact]
    public void Create_RejectsEmptyTenantId()
    {
        var action = () => WorkItemHistoryEntry.Create(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "Status", "Backlog", "Done", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Create_RejectsEmptyWorkItemId()
    {
        var action = () => WorkItemHistoryEntry.Create(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), "Status", "Backlog", "Done", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Create_RejectsEmptyChangedByMembershipId()
    {
        var action = () => WorkItemHistoryEntry.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "Status", "Backlog", "Done", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Create_RejectsEmptyFieldName()
    {
        var action = () => WorkItemHistoryEntry.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "  ", "Backlog", "Done", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }
}
