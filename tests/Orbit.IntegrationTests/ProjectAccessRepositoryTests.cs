using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Api.Tenancy;
using Orbit.Application.Abstractions;
using Orbit.Domain.Access;
using Orbit.Domain.Directory;
using Orbit.Domain.Identity;
using Orbit.Domain.Projects;
using Orbit.Infrastructure.Persistence;

namespace Orbit.IntegrationTests;

/// <summary>
/// Exercises the <see cref="IProjectAccessRepository"/> registered implementation against real
/// Postgres so the nested EF `Any` subqueries across
/// TenantMembership/ProjectRoleAssignment/ProjectGroupRoleAssignment/RolePermission/GroupMembership
/// actually translate to SQL correctly, covering every access path the close-sprint notification
/// fan-out (§13.5) relies on. Sets the scope's <see cref="TenantContext"/> directly (bypassing
/// the HTTP tenant-header pipeline) so the DbContext's tenant query filters see this test's
/// generated tenant id, the same seam <c>TenantTransactionMiddleware</c> uses per-request.
/// </summary>
public sealed class ProjectAccessRepositoryTests : IClassFixture<OrbitApiFactory>
{
    private readonly OrbitApiFactory _factory;

    public ProjectAccessRepositoryTests(OrbitApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ListUserIdsWithPermissionAsync_ReturnsTenantWideExplicitAndGroupGrantees_ExcludingOthers()
    {
        using var scope = _factory.Services.CreateScope();
        var tenantId = Guid.NewGuid();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenantId);
        var dbContext = scope.ServiceProvider.GetRequiredService<OrbitDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IProjectAccessRepository>();
        var now = DateTimeOffset.UtcNow;

        var project = Project.Create(tenantId, "ORB", "Orbit", now);
        var otherProject = Project.Create(tenantId, "OTH", "Other", now);
        await dbContext.Projects.AddRangeAsync(project, otherProject);

        var viewerRole = Role.Create(tenantId, "Viewer", isSystem: false, [ProjectPermission.View], now);
        var noViewRole = Role.Create(tenantId, "NoView", isSystem: false, [ProjectPermission.CreateWorkItem], now);
        await dbContext.Roles.AddRangeAsync(viewerRole, noViewRole);

        var ownerAccount = UserAccount.Create($"owner-{Guid.NewGuid()}@example.test", "Owner", now);
        var explicitViewerAccount = UserAccount.Create($"explicit-{Guid.NewGuid()}@example.test", "Explicit Viewer", now);
        var groupViewerAccount = UserAccount.Create($"group-{Guid.NewGuid()}@example.test", "Group Viewer", now);
        var noPermissionAccount = UserAccount.Create($"noperm-{Guid.NewGuid()}@example.test", "No Permission", now);
        var otherProjectAccount = UserAccount.Create($"other-{Guid.NewGuid()}@example.test", "Other Project Viewer", now);
        await dbContext.UserAccounts.AddRangeAsync(
            ownerAccount, explicitViewerAccount, groupViewerAccount, noPermissionAccount, otherProjectAccount);

        var owner = TenantMembership.CreateForUser(tenantId, ownerAccount.Id, TenantRole.Owner, now);
        var explicitViewer = TenantMembership.CreateForUser(tenantId, explicitViewerAccount.Id, TenantRole.Member, now);
        var groupViewer = TenantMembership.CreateForUser(tenantId, groupViewerAccount.Id, TenantRole.Member, now);
        var noPermissionMember = TenantMembership.CreateForUser(tenantId, noPermissionAccount.Id, TenantRole.Member, now);
        var otherProjectViewer = TenantMembership.CreateForUser(tenantId, otherProjectAccount.Id, TenantRole.Member, now);
        await dbContext.TenantMemberships.AddRangeAsync(
            owner, explicitViewer, groupViewer, noPermissionMember, otherProjectViewer);

        var explicitAssignment = ProjectRoleAssignment.Create(tenantId, project.Id, explicitViewer.Id, viewerRole.Id, now);
        var noPermissionAssignment = ProjectRoleAssignment.Create(tenantId, project.Id, noPermissionMember.Id, noViewRole.Id, now);
        var otherProjectAssignment = ProjectRoleAssignment.Create(tenantId, otherProject.Id, otherProjectViewer.Id, viewerRole.Id, now);
        await dbContext.ProjectRoleAssignments.AddRangeAsync(
            explicitAssignment, noPermissionAssignment, otherProjectAssignment);

        var group = DirectoryGroup.Create(tenantId, "Viewers Group", owner.Id, now);
        await dbContext.DirectoryGroups.AddAsync(group);
        var groupMembership = GroupMembership.Create(tenantId, group.Id, groupViewer.Id, now);
        await dbContext.GroupMemberships.AddAsync(groupMembership);
        var groupAssignment = ProjectGroupRoleAssignment.Create(tenantId, project.Id, group.Id, viewerRole.Id, now);
        await dbContext.ProjectGroupRoleAssignments.AddAsync(groupAssignment);

        await dbContext.SaveChangesAsync();

        var result = await repository.ListUserIdsWithPermissionAsync(
            tenantId, project.Id, ProjectPermission.View, CancellationToken.None);

        Assert.Contains(owner.UserId!.Value, result);
        Assert.Contains(explicitViewer.UserId!.Value, result);
        Assert.Contains(groupViewer.UserId!.Value, result);
        Assert.DoesNotContain(noPermissionMember.UserId!.Value, result);
        Assert.DoesNotContain(otherProjectViewer.UserId!.Value, result);
        Assert.Equal(3, result.Count);
    }
}
