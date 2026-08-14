using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Directory;
using Orbit.Domain.Identity;
using Orbit.Domain.Messaging;
using Orbit.Domain.Projects;
using Orbit.Domain.Settings;
using Orbit.Domain.WorkItems;
using Orbit.Infrastructure.Persistence;

namespace Orbit.ArchitectureTests;

public sealed class PersistenceSecurityTests
{
    [Fact]
    public void EveryTenantEntityHasAQueryFilter()
    {
        using var dbContext = CreateContext();
        Type[] tenantEntities =
        [
            typeof(Project),
            typeof(WorkItem),
            typeof(TenantMembership),
            typeof(ProjectRoleAssignment),
            typeof(ProjectGroupRoleAssignment),
            typeof(WorkspaceSetting),
            typeof(ProjectSetting),
            typeof(Team),
            typeof(TeamMembership),
            typeof(DirectoryGroup),
            typeof(GroupMembership),
            typeof(Board),
            typeof(Sprint),
            typeof(SprintMembership),
            typeof(SprintCompletionOperation),
            typeof(SprintScopeFact)
        ];

        foreach (var entity in tenantEntities)
        {
            Assert.NotEmpty(dbContext.Model.FindEntityType(entity)!.GetDeclaredQueryFilters());
        }
    }

    [Fact]
    public void GlobalIdentityAndOutboxEntitiesHaveNoQueryFilter()
    {
        using var dbContext = CreateContext();
        Type[] globalEntities =
        [
            typeof(UserAccount),
            typeof(ExternalIdentity),
            typeof(LocalCredential),
            typeof(RefreshSession),
            typeof(PasswordResetToken),
            typeof(OutboxEmailMessage)
        ];

        foreach (var entity in globalEntities)
        {
            Assert.Empty(dbContext.Model.FindEntityType(entity)!.GetDeclaredQueryFilters());
        }
    }

    [Fact]
    public void RefreshSessionVersionIsAConcurrencyToken()
    {
        using var dbContext = CreateContext();

        var version = dbContext.Model.FindEntityType(typeof(RefreshSession))?.FindProperty(nameof(RefreshSession.Version));

        Assert.NotNull(version);
        Assert.True(version.IsConcurrencyToken);
    }

    private static OrbitDbContext CreateContext() =>
        new(
            new DbContextOptionsBuilder<OrbitDbContext>()
                .UseNpgsql("Host=localhost;Database=orbit_model_tests")
                .Options,
            new TenantContextStub());

    private sealed class TenantContextStub : ITenantContext
    {
        public Guid TenantId { get; } = Guid.NewGuid();
    }
}
