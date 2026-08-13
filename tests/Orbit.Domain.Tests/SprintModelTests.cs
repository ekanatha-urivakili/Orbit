using Orbit.Domain.Boards;
using Orbit.Domain.Choices;
using Orbit.Domain.Common;

namespace Orbit.Domain.Tests;

public sealed class SprintModelTests
{
    [Fact]
    public void Sprint_Create_TrimsNameAndDefaultsToFuture()
    {
        var sprint = Sprint.Create(Guid.NewGuid(), Guid.NewGuid(), "  Sprint 1  ", DateTimeOffset.UtcNow);

        Assert.Equal("Sprint 1", sprint.Name);
        Assert.Equal(SprintState.Future, sprint.State);
        Assert.Equal(1, sprint.Version);
    }

    [Fact]
    public void Sprint_Create_RejectsTooShortName()
    {
        var action = () => Sprint.Create(Guid.NewGuid(), Guid.NewGuid(), "A", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Sprint_Create_RejectsEmptyIdentifiers()
    {
        var action = () => Sprint.Create(Guid.Empty, Guid.NewGuid(), "Sprint 1", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Sprint_Start_TransitionsToActiveAndBumpsVersion()
    {
        var now = DateTimeOffset.UtcNow;
        var sprint = Sprint.Create(Guid.NewGuid(), Guid.NewGuid(), "Sprint 1", now);
        var startDate = DateOnly.FromDateTime(now.UtcDateTime);
        var endDate = startDate.AddDays(14);

        sprint.Start("Ship the thing", startDate, endDate, now.AddMinutes(5));

        Assert.Equal(SprintState.Active, sprint.State);
        Assert.Equal("Ship the thing", sprint.Goal);
        Assert.Equal(startDate, sprint.StartDate);
        Assert.Equal(endDate, sprint.EndDate);
        Assert.Equal(2, sprint.Version);
        Assert.Equal(now.AddMinutes(5), sprint.UpdatedAt);
    }

    [Fact]
    public void Sprint_Start_DefaultsStartDateToToday_WhenNotSupplied()
    {
        var now = DateTimeOffset.UtcNow;
        var sprint = Sprint.Create(Guid.NewGuid(), Guid.NewGuid(), "Sprint 1", now);

        sprint.Start(null, null, null, now);

        Assert.Equal(DateOnly.FromDateTime(now.UtcDateTime), sprint.StartDate);
    }

    [Fact]
    public void Sprint_Start_RejectsEndDateBeforeStartDate()
    {
        var now = DateTimeOffset.UtcNow;
        var sprint = Sprint.Create(Guid.NewGuid(), Guid.NewGuid(), "Sprint 1", now);
        var startDate = DateOnly.FromDateTime(now.UtcDateTime);

        var action = () => sprint.Start(null, startDate, startDate.AddDays(-1), now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Sprint_Start_RejectsNonFutureSprint()
    {
        var now = DateTimeOffset.UtcNow;
        var sprint = Sprint.Create(Guid.NewGuid(), Guid.NewGuid(), "Sprint 1", now);
        sprint.Start(null, null, null, now);

        var action = () => sprint.Start(null, null, null, now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Sprint_StartClosing_TransitionsToClosingAndBumpsVersion()
    {
        var now = DateTimeOffset.UtcNow;
        var sprint = Sprint.Create(Guid.NewGuid(), Guid.NewGuid(), "Sprint 1", now);
        sprint.Start(null, null, null, now);

        sprint.StartClosing(now.AddDays(14));

        Assert.Equal(SprintState.Closing, sprint.State);
        Assert.Equal(3, sprint.Version);
    }

    [Fact]
    public void Sprint_StartClosing_RejectsFutureSprint()
    {
        var now = DateTimeOffset.UtcNow;
        var sprint = Sprint.Create(Guid.NewGuid(), Guid.NewGuid(), "Sprint 1", now);

        var action = () => sprint.StartClosing(now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Sprint_StartClosing_AllowsReopenedSprint()
    {
        var now = DateTimeOffset.UtcNow;
        var sprint = Sprint.Create(Guid.NewGuid(), Guid.NewGuid(), "Sprint 1", now);
        sprint.Start(null, null, null, now);
        sprint.StartClosing(now);
        sprint.FinishClosing(now);
        sprint.Reopen(now);

        sprint.StartClosing(now);

        Assert.Equal(SprintState.Closing, sprint.State);
    }

    [Fact]
    public void Sprint_FinishClosing_TransitionsToClosedAndBumpsVersion()
    {
        var now = DateTimeOffset.UtcNow;
        var sprint = Sprint.Create(Guid.NewGuid(), Guid.NewGuid(), "Sprint 1", now);
        sprint.Start(null, null, null, now);
        sprint.StartClosing(now);

        sprint.FinishClosing(now.AddDays(14));

        Assert.Equal(SprintState.Closed, sprint.State);
        Assert.Equal(DateOnly.FromDateTime(now.AddDays(14).UtcDateTime), sprint.EndDate);
        Assert.Equal(4, sprint.Version);
    }

    [Fact]
    public void Sprint_FinishClosing_RejectsNonClosingSprint()
    {
        var now = DateTimeOffset.UtcNow;
        var sprint = Sprint.Create(Guid.NewGuid(), Guid.NewGuid(), "Sprint 1", now);

        var action = () => sprint.FinishClosing(now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Sprint_Reopen_TransitionsToReopenedAndBumpsVersion()
    {
        var now = DateTimeOffset.UtcNow;
        var sprint = Sprint.Create(Guid.NewGuid(), Guid.NewGuid(), "Sprint 1", now);
        sprint.Start(null, null, null, now);
        sprint.StartClosing(now);
        sprint.FinishClosing(now);

        sprint.Reopen(now);

        Assert.Equal(SprintState.Reopened, sprint.State);
        Assert.Equal(5, sprint.Version);
    }

    [Fact]
    public void Sprint_Reopen_RejectsNonClosedSprint()
    {
        var now = DateTimeOffset.UtcNow;
        var sprint = Sprint.Create(Guid.NewGuid(), Guid.NewGuid(), "Sprint 1", now);

        var action = () => sprint.Reopen(now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void SprintMembership_Create_RejectsEmptyIdentifiers()
    {
        var action = () => SprintMembership.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void SprintMembership_Remove_SetsRemovedAt()
    {
        var now = DateTimeOffset.UtcNow;
        var membership = SprintMembership.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now);

        membership.Remove(now.AddMinutes(1));

        Assert.Equal(now.AddMinutes(1), membership.RemovedAt);
    }

    [Fact]
    public void SprintMembership_Remove_IsIdempotent()
    {
        var now = DateTimeOffset.UtcNow;
        var membership = SprintMembership.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now);

        membership.Remove(now.AddMinutes(1));
        membership.Remove(now.AddMinutes(5));

        Assert.Equal(now.AddMinutes(1), membership.RemovedAt);
    }

    [Fact]
    public void SprintCompletionOperation_Create_RejectsEmptyIdentifiers()
    {
        var action = () => SprintCompletionOperation.Create(Guid.Empty, Guid.NewGuid(), null, 0, DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void SprintCompletionOperation_Create_RejectsNegativeTotalCount()
    {
        var action = () =>
            SprintCompletionOperation.Create(Guid.NewGuid(), Guid.NewGuid(), null, -1, DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void SprintCompletionOperation_RecordProgress_RejectsMovingBackward()
    {
        var now = DateTimeOffset.UtcNow;
        var operation = SprintCompletionOperation.Create(Guid.NewGuid(), Guid.NewGuid(), null, 5, now);
        operation.RecordProgress(3, now);

        var action = () => operation.RecordProgress(2, now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void SprintCompletionOperation_MarkCompleted_SetsCompletedState()
    {
        var now = DateTimeOffset.UtcNow;
        var operation = SprintCompletionOperation.Create(Guid.NewGuid(), Guid.NewGuid(), null, 5, now);

        operation.MarkCompleted(now);

        Assert.Equal(SprintCompletionOperationState.Completed, operation.State);
    }

    [Fact]
    public void SprintScopeFact_Create_RejectsEmptyIdentifiers()
    {
        var now = DateTimeOffset.UtcNow;
        var action = () =>
            SprintScopeFact.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), AgileFactType.SprintAdded, now, now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void SprintScopeFact_Create_AllowsNullWorkItemId_ForSprintLevelFacts()
    {
        var now = DateTimeOffset.UtcNow;

        var fact = SprintScopeFact.Create(Guid.NewGuid(), Guid.NewGuid(), null, AgileFactType.SprintCompleted, now, now);

        Assert.Null(fact.WorkItemId);
        Assert.Equal(AgileFactType.SprintCompleted, fact.FactType);
    }
}
