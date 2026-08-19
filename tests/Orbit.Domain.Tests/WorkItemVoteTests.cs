using Orbit.Domain.Common;
using Orbit.Domain.WorkItems;

namespace Orbit.Domain.Tests;

public sealed class WorkItemVoteTests
{
    [Fact]
    public void Create_AssignsIdentity()
    {
        var tenantId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var vote = WorkItemVote.Create(tenantId, workItemId, userId, DateTimeOffset.UtcNow);

        Assert.Equal(tenantId, vote.TenantId);
        Assert.Equal(workItemId, vote.WorkItemId);
        Assert.Equal(userId, vote.UserId);
    }

    [Fact]
    public void Create_RejectsEmptyIds()
    {
        var action = () => WorkItemVote.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }
}
