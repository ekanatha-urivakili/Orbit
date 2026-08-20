using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Configuration;
using Orbit.Domain.Directory;
using Orbit.Domain.Identity;
using Orbit.Domain.Idempotency;
using Orbit.Domain.Integrations;
using Orbit.Domain.Messaging;
using Orbit.Domain.Organizations;
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
    public DbSet<WorkItemLink> WorkItemLinks => Set<WorkItemLink>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<ProjectRoleAssignment> ProjectRoleAssignments => Set<ProjectRoleAssignment>();
    public DbSet<ProjectGroupRoleAssignment> ProjectGroupRoleAssignments => Set<ProjectGroupRoleAssignment>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();
    public DbSet<LocalCredential> LocalCredentials => Set<LocalCredential>();
    public DbSet<SiteRoleAssignment> SiteRoleAssignments => Set<SiteRoleAssignment>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<GoogleSignInHandoff> GoogleSignInHandoffs => Set<GoogleSignInHandoff>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<WorkspaceSetting> WorkspaceSettings => Set<WorkspaceSetting>();
    public DbSet<WorkspaceTypographySetting> WorkspaceTypographySettings => Set<WorkspaceTypographySetting>();
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
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<ServiceAccountCredential> ServiceAccountCredentials => Set<ServiceAccountCredential>();
    public DbSet<OutboxEmailMessage> OutboxEmailMessages => Set<OutboxEmailMessage>();
    public DbSet<AttachmentScanRequest> AttachmentScanRequests => Set<AttachmentScanRequest>();
    public DbSet<WorkspaceInvitation> WorkspaceInvitations => Set<WorkspaceInvitation>();
    public DbSet<WorkItemTypeDefinition> WorkItemTypeDefinitions => Set<WorkItemTypeDefinition>();
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<WorkItemComment> WorkItemComments => Set<WorkItemComment>();
    public DbSet<WorkItemHistoryEntry> WorkItemHistoryEntries => Set<WorkItemHistoryEntry>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<WorkItemWatcher> WorkItemWatchers => Set<WorkItemWatcher>();
    public DbSet<WorkItemVote> WorkItemVotes => Set<WorkItemVote>();
    public DbSet<WorkItemWorklog> WorkItemWorklogs => Set<WorkItemWorklog>();
    public DbSet<SlackConnection> SlackConnections => Set<SlackConnection>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrbitDbContext).Assembly);

        modelBuilder.Entity<Project>().HasQueryFilter(project => project.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<WorkItem>().HasQueryFilter(workItem => workItem.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<WorkItemLink>().HasQueryFilter(link => link.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<TenantMembership>()
            .HasQueryFilter(membership => membership.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<ProjectRoleAssignment>()
            .HasQueryFilter(assignment => assignment.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<ProjectGroupRoleAssignment>()
            .HasQueryFilter(assignment => assignment.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<WorkspaceSetting>()
            .HasQueryFilter(setting => setting.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<WorkspaceTypographySetting>()
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
        modelBuilder.Entity<WorkspaceInvitation>()
            .HasQueryFilter(invitation => invitation.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<WorkItemTypeDefinition>()
            .HasQueryFilter(definition => definition.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<CustomFieldDefinition>()
            .HasQueryFilter(definition => definition.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<WorkItemComment>()
            .HasQueryFilter(comment => comment.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<WorkItemHistoryEntry>()
            .HasQueryFilter(entry => entry.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<Attachment>()
            .HasQueryFilter(attachment => attachment.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<WorkItemWatcher>()
            .HasQueryFilter(watcher => watcher.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<WorkItemVote>()
            .HasQueryFilter(vote => vote.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<WorkItemWorklog>()
            .HasQueryFilter(worklog => worklog.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<SlackConnection>()
            .HasQueryFilter(connection => connection.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<IdempotencyRecord>()
            .HasQueryFilter(record => record.TenantId == tenantContext.TenantId);
    }
}
