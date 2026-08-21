using Orbit.Domain.Access;
using Orbit.Domain.Common;

namespace Orbit.Domain.Tests;

public sealed class RoleTests
{
    [Fact]
    public void Create_RejectsEmptyTenantId()
    {
        var action = () => Role.Create(Guid.Empty, "Reviewer", isSystem: false, [ProjectPermission.View], DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_RejectsBlankName(string name)
    {
        var action = () => Role.Create(Guid.NewGuid(), name, isSystem: false, [], DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Create_DeduplicatesPermissions()
    {
        var role = Role.Create(
            Guid.NewGuid(), "Reviewer", isSystem: false,
            [ProjectPermission.View, ProjectPermission.View],
            DateTimeOffset.UtcNow);

        Assert.Single(role.Permissions);
    }

    [Fact]
    public void GrantPermission_AddsPermissionOnce()
    {
        var role = Role.Create(Guid.NewGuid(), "Reviewer", isSystem: false, [], DateTimeOffset.UtcNow);

        role.GrantPermission(ProjectPermission.View);
        role.GrantPermission(ProjectPermission.View);

        Assert.Single(role.Permissions);
    }

    [Fact]
    public void RevokePermission_RemovesPermission()
    {
        var role = Role.Create(Guid.NewGuid(), "Reviewer", isSystem: false, [ProjectPermission.View], DateTimeOffset.UtcNow);

        role.RevokePermission(ProjectPermission.View);

        Assert.Empty(role.Permissions);
    }

    [Fact]
    public void Rename_RejectsSystemRole()
    {
        var role = Role.Create(Guid.NewGuid(), "Administrator", isSystem: true, [], DateTimeOffset.UtcNow);

        var action = () => role.Rename("Renamed");

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Rename_AllowsNonSystemRole()
    {
        var role = Role.Create(Guid.NewGuid(), "Reviewer", isSystem: false, [], DateTimeOffset.UtcNow);

        role.Rename("Reviewer v2");

        Assert.Equal("Reviewer v2", role.Name);
    }

    [Fact]
    public void SeedSystemRoles_ReproducesLegacyProjectRolePermissions()
    {
        var roles = Role.SeedSystemRoles(Guid.NewGuid(), DateTimeOffset.UtcNow);

        var administrator = Assert.Single(roles, role => role.Name == "Administrator");
        var member = Assert.Single(roles, role => role.Name == "Member");
        var viewer = Assert.Single(roles, role => role.Name == "Viewer");

        Assert.Equal(4, administrator.Permissions.Count);
        Assert.Equal(
            [ProjectPermission.View, ProjectPermission.CreateWorkItem, ProjectPermission.TransitionWorkItem],
            member.Permissions.Select(p => p.Permission).OrderBy(p => p));
        Assert.Equal([ProjectPermission.View], viewer.Permissions.Select(p => p.Permission));
        Assert.All(roles, role => Assert.True(role.IsSystem));
    }
}
