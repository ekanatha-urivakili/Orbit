using Orbit.Domain.Workspaces;

namespace Orbit.Domain.Tests;

public sealed class WorkspaceModelTests
{
    [Fact]
    public void Workspace_Create_StartsAtEpochOne()
    {
        var workspace = Workspace.Create("Orbit", DateTimeOffset.UtcNow);

        Assert.Equal(1, workspace.AuthorizationEpoch);
    }

    [Fact]
    public void Workspace_IncrementAuthorizationEpoch_IncreasesByOne()
    {
        var workspace = Workspace.Create("Orbit", DateTimeOffset.UtcNow);

        workspace.IncrementAuthorizationEpoch();
        workspace.IncrementAuthorizationEpoch();

        Assert.Equal(3, workspace.AuthorizationEpoch);
    }
}
