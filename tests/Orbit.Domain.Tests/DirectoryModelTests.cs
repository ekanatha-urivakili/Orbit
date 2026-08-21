using Orbit.Domain.Access;
using Orbit.Domain.Common;
using Orbit.Domain.Directory;

namespace Orbit.Domain.Tests;

public sealed class DirectoryModelTests
{
    [Fact]
    public void Team_Create_TrimsName()
    {
        var team = Team.Create(Guid.NewGuid(), "  Platform Team  ", Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal("Platform Team", team.Name);
    }

    [Fact]
    public void Team_Create_RejectsTooShortName()
    {
        var action = () => Team.Create(Guid.NewGuid(), "A", Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Team_Rename_UpdatesNameAndTimestamp()
    {
        var now = DateTimeOffset.UtcNow;
        var team = Team.Create(Guid.NewGuid(), "Original", Guid.NewGuid(), now);

        team.Rename("Renamed", now.AddMinutes(5));

        Assert.Equal("Renamed", team.Name);
        Assert.Equal(now.AddMinutes(5), team.UpdatedAt);
    }

    [Fact]
    public void TeamMembership_Create_RejectsEmptyIdentifiers()
    {
        var action = () => TeamMembership.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void DirectoryGroup_Create_TrimsName()
    {
        var group = DirectoryGroup.Create(Guid.NewGuid(), "  Platform Group  ", Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal("Platform Group", group.Name);
    }

    [Fact]
    public void DirectoryGroup_Create_RejectsTooShortName()
    {
        var action = () => DirectoryGroup.Create(Guid.NewGuid(), "A", Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void DirectoryGroup_Rename_UpdatesNameAndTimestamp()
    {
        var now = DateTimeOffset.UtcNow;
        var group = DirectoryGroup.Create(Guid.NewGuid(), "Original", Guid.NewGuid(), now);

        group.Rename("Renamed", now.AddMinutes(5));

        Assert.Equal("Renamed", group.Name);
        Assert.Equal(now.AddMinutes(5), group.UpdatedAt);
    }

    [Fact]
    public void GroupMembership_Create_RejectsEmptyIdentifiers()
    {
        var action = () => GroupMembership.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void ProjectGroupRoleAssignment_Create_RejectsEmptyIdentifiers()
    {
        var action = () => ProjectGroupRoleAssignment.Create(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void ProjectGroupRoleAssignment_ChangeRole_UpdatesRole()
    {
        var assignment = ProjectGroupRoleAssignment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var roleId = Guid.NewGuid();

        assignment.ChangeRole(roleId);

        Assert.Equal(roleId, assignment.RoleId);
    }
}
