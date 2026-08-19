using Orbit.Domain.Common;
using Orbit.Domain.WorkItems;

namespace Orbit.Domain.Tests;

public sealed class WorkItemWorklogTests
{
    [Fact]
    public void Create_AssignsFields()
    {
        var tenantId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();
        var authorMembershipId = Guid.NewGuid();
        var workDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var worklog = WorkItemWorklog.Create(
            tenantId, workItemId, authorMembershipId, 90, workDate, "Investigated the issue",
            DateTimeOffset.UtcNow);

        Assert.Equal(90, worklog.MinutesSpent);
        Assert.Equal(workDate, worklog.WorkDate);
        Assert.Equal("Investigated the issue", worklog.Description);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1441)]
    public void Create_RejectsOutOfRangeMinutes(int minutes)
    {
        var action = () => WorkItemWorklog.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), minutes, DateOnly.FromDateTime(DateTime.UtcNow),
            null, DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Create_AllowsNullDescription()
    {
        var worklog = WorkItemWorklog.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 30, DateOnly.FromDateTime(DateTime.UtcNow), null,
            DateTimeOffset.UtcNow);

        Assert.Null(worklog.Description);
    }
}
