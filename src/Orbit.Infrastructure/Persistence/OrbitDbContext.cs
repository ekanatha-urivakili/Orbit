using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Directory;
using Orbit.Domain.Identity;
using Orbit.Domain.Projects;
using Orbit.Domain.Settings;
using Orbit.Domain.WorkItems;
using Orbit.Domain.Workspaces;

namespace Orbit.Infrastructure.Persistence;

public sealed class OrbitDbContext(
    DbContextOptions<OrbitDbContext> options,
    ITenantContext tenantContext) : DbContext(options), IUnitOfWork
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<ProjectRoleAssignment> ProjectRoleAssignments => Set<ProjectRoleAssignment>();
    public DbSet<ProjectGroupRoleAssignment> ProjectGroupRoleAssignments => Set<ProjectGroupRoleAssignment>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();
    public DbSet<LocalCredential> LocalCredentials => Set<LocalCredential>();
    public DbSet<SiteRoleAssignment> SiteRoleAssignments => Set<SiteRoleAssignment>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<WorkspaceSetting> WorkspaceSettings => Set<WorkspaceSetting>();
    public DbSet<ProjectSetting> ProjectSettings => Set<ProjectSetting>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();
    public DbSet<DirectoryGroup> DirectoryGroups => Set<DirectoryGroup>();
    public DbSet<GroupMembership> GroupMemberships => Set<GroupMembership>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<SprintMembership> SprintMemberships => Set<SprintMembership>();
    public DbSet<SprintCompletionOperation> SprintCompletionOperations => Set<SprintCompletionOperation>();
    public DbSet<SprintScopeFact> SprintScopeFacts => Set<SprintScopeFact>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrbitDbContext).Assembly);

        modelBuilder.Entity<Project>().HasQueryFilter(project => project.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<WorkItem>().HasQueryFilter(workItem => workItem.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<TenantMembership>()
            .HasQueryFilter(membership => membership.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<ProjectRoleAssignment>()
            .HasQueryFilter(assignment => assignment.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<ProjectGroupRoleAssignment>()
            .HasQueryFilter(assignment => assignment.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<WorkspaceSetting>()
            .HasQueryFilter(setting => setting.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<ProjectSetting>()
            .HasQueryFilter(setting => setting.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<Team>().HasQueryFilter(team => team.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<TeamMembership>()
            .HasQueryFilter(membership => membership.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<DirectoryGroup>().HasQueryFilter(group => group.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<GroupMembership>()
            .HasQueryFilter(membership => membership.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<Board>().HasQueryFilter(board => board.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<Sprint>().HasQueryFilter(sprint => sprint.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<SprintMembership>()
            .HasQueryFilter(membership => membership.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<SprintCompletionOperation>()
            .HasQueryFilter(operation => operation.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<SprintScopeFact>().HasQueryFilter(fact => fact.TenantId == tenantContext.TenantId);
    }
}
