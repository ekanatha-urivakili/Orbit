using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Directory;
using Orbit.Domain.Identity;
using Orbit.Domain.Projects;
using Orbit.Domain.Settings;
using Orbit.Domain.WorkItems;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Abstractions;

public interface ITenantContext
{
    Guid TenantId { get; }
}

public interface ICurrentPrincipal
{
    Guid? UserId { get; }
    Guid? SessionId { get; }
    Guid MembershipId { get; }
    PrincipalType PrincipalType { get; }
    TenantRole TenantRole { get; }
    bool IsDevelopmentBypass { get; }
}

public interface ITenantMembershipRepository
{
    Task AddAsync(TenantMembership membership, CancellationToken cancellationToken);
    Task<TenantMembership?> GetActiveAsync(
        Guid tenantId,
        string issuer,
        string subject,
        CancellationToken cancellationToken);
    Task<TenantMembership?> GetActiveByUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken);
    Task<TenantMembership?> GetActiveAsync(
        Guid tenantId,
        Guid membershipId,
        CancellationToken cancellationToken);
    Task<TenantMembership?> GetOwnerAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TenantMembership>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
}

public interface ISettingsRepository
{
    Task<UserAccount?> GetUserAccountAsync(Guid userId, CancellationToken cancellationToken);
    Task<UserPreference?> GetUserPreferenceAsync(Guid userId, CancellationToken cancellationToken);
    Task<NotificationPreference?> GetNotificationPreferenceAsync(Guid userId, CancellationToken cancellationToken);
    Task<Workspace?> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<WorkspaceSetting?> GetWorkspaceSettingAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<ProjectSetting?> GetProjectSettingAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken);
    Task AddUserPreferenceAsync(UserPreference preference, CancellationToken cancellationToken);
    Task AddNotificationPreferenceAsync(NotificationPreference preference, CancellationToken cancellationToken);
    Task AddWorkspaceSettingAsync(WorkspaceSetting setting, CancellationToken cancellationToken);
    Task AddProjectSettingAsync(ProjectSetting setting, CancellationToken cancellationToken);
}

public sealed record PasswordHash(
    string Value,
    string Algorithm,
    int ParametersVersion);

public interface IPasswordHasher
{
    Task<PasswordHash> HashAsync(string password, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies a password against an encoded hash. Must take substantially the same amount of
    /// work whether or not <paramref name="encodedHash"/> is null, so callers can run it against a
    /// fixed dummy hash for unknown accounts and keep login timing enumeration-resistant (NFR-17).
    /// </summary>
    Task<bool> VerifyAsync(string password, string? encodedHash, CancellationToken cancellationToken);
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public sealed record VerifiedExternalIdentity(string Issuer, string Subject);

public interface IExternalIdentityTokenValidator
{
    Task<VerifiedExternalIdentity> ValidateAsync(string token, CancellationToken cancellationToken);
}

public interface IAccessTokenIssuer
{
    TimeSpan RefreshTokenLifetime { get; }

    AccessToken IssueUserToken(Guid userId, Guid tenantId, Guid sessionId, DateTimeOffset now);
}

public interface IAuthenticationRepository
{
    Task<UserAccount?> GetUserAccountAsync(Guid userId, CancellationToken cancellationToken);
    Task<UserAccount?> GetUserAccountByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<LocalCredential?> GetLocalCredentialAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TenantMembership>> ListActiveMembershipsByUserAsync(
        Guid userId,
        CancellationToken cancellationToken);
    Task<Workspace?> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken);
    Task AddRefreshSessionAsync(RefreshSession session, CancellationToken cancellationToken);
    Task<RefreshSession?> GetRefreshSessionByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task<RefreshSession?> GetActiveSessionAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RefreshSession>> ListActiveSessionsByUserAsync(
        Guid userId,
        CancellationToken cancellationToken);
    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken);
    Task AddExternalIdentityAsync(ExternalIdentity identity, CancellationToken cancellationToken);
    Task<ExternalIdentity?> GetExternalIdentityAsync(string issuer, string subject, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExternalIdentity>> ListExternalIdentitiesByUserAsync(
        Guid userId,
        CancellationToken cancellationToken);
    Task<ExternalIdentity?> GetExternalIdentityAsync(Guid id, Guid userId, CancellationToken cancellationToken);
    Task RemoveExternalIdentityAsync(ExternalIdentity identity, CancellationToken cancellationToken);
}

public interface IBootstrapRepository
{
    Task<bool> IsInitializationRequiredAsync(CancellationToken cancellationToken);
    Task<bool> TryInitializeAsync(
        UserAccount account,
        LocalCredential credential,
        SiteRoleAssignment siteRole,
        Workspace workspace,
        TenantMembership ownerMembership,
        CancellationToken cancellationToken);
}

public interface ITenantAuthorization
{
    bool CanCreateProject();
    bool CanCreateMembership(TenantRole role);
    bool CanManageTeams();
}

public interface ITeamRepository
{
    Task AddAsync(Team team, CancellationToken cancellationToken);
    Task<Team?> GetAsync(Guid tenantId, Guid teamId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Team>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
}

public interface ITeamMembershipRepository
{
    Task AddAsync(TeamMembership membership, CancellationToken cancellationToken);
    Task RemoveAsync(TeamMembership membership, CancellationToken cancellationToken);
    Task<TeamMembership?> GetAsync(
        Guid tenantId,
        Guid teamId,
        Guid membershipId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<TeamMembership>> ListByTeamAsync(
        Guid tenantId,
        Guid teamId,
        CancellationToken cancellationToken);
}

public interface IDirectoryGroupRepository
{
    Task AddAsync(DirectoryGroup group, CancellationToken cancellationToken);
    Task<DirectoryGroup?> GetAsync(Guid tenantId, Guid groupId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DirectoryGroup>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
}

public interface IGroupMembershipRepository
{
    Task AddAsync(GroupMembership membership, CancellationToken cancellationToken);
    Task RemoveAsync(GroupMembership membership, CancellationToken cancellationToken);
    Task<GroupMembership?> GetAsync(
        Guid tenantId,
        Guid groupId,
        Guid membershipId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<GroupMembership>> ListByGroupAsync(
        Guid tenantId,
        Guid groupId,
        CancellationToken cancellationToken);
}

public interface IProjectRoleRepository
{
    Task<ProjectRoleAssignment?> GetAsync(
        Guid tenantId,
        Guid projectId,
        Guid membershipId,
        CancellationToken cancellationToken);
    Task AddAsync(ProjectRoleAssignment assignment, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectRoleAssignment>> ListByProjectAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken);
}

public interface IProjectGroupRoleRepository
{
    Task<ProjectGroupRoleAssignment?> GetAsync(
        Guid tenantId,
        Guid projectId,
        Guid groupId,
        CancellationToken cancellationToken);
    Task AddAsync(ProjectGroupRoleAssignment assignment, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectGroupRoleAssignment>> ListByProjectAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken);
}

public interface IProjectRepository
{
    Task AddAsync(Project project, CancellationToken cancellationToken);
    Task<Project?> GetAsync(
        Guid tenantId,
        Guid projectId,
        ProjectPermission permission,
        CancellationToken cancellationToken);
    Task<PagedResult<Project>> ListAsync(
        Guid tenantId,
        ProjectPermission permission,
        int skip,
        int take,
        CancellationToken cancellationToken);
}

public interface IBoardRepository
{
    Task AddAsync(Board board, CancellationToken cancellationToken);
    Task<Board?> GetAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken);
}

public interface ISprintRepository
{
    Task AddAsync(Sprint sprint, CancellationToken cancellationToken);
    Task<Sprint?> GetAsync(Guid tenantId, Guid sprintId, CancellationToken cancellationToken);
    Task<Sprint?> GetActiveAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Sprint>> ListByProjectAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken);
}

public interface ISprintMembershipRepository
{
    Task AddAsync(SprintMembership membership, CancellationToken cancellationToken);
    Task<SprintMembership?> GetCurrentByWorkItemAsync(
        Guid tenantId,
        Guid workItemId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<SprintMembership>> ListCurrentBySprintAsync(
        Guid tenantId,
        Guid sprintId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<SprintMembership>> ListCurrentBySprintsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> sprintIds,
        CancellationToken cancellationToken);
}

public interface ISprintCompletionOperationRepository
{
    Task AddAsync(SprintCompletionOperation operation, CancellationToken cancellationToken);
    Task<SprintCompletionOperation?> GetAsync(Guid tenantId, Guid sprintId, CancellationToken cancellationToken);
}

public interface ISprintScopeFactRepository
{
    Task AddAsync(SprintScopeFact fact, CancellationToken cancellationToken);
}

public interface IWorkItemRepository
{
    Task AddAsync(WorkItem workItem, CancellationToken cancellationToken);
    Task<WorkItem?> GetAsync(
        Guid tenantId,
        Guid workItemId,
        ProjectPermission permission,
        CancellationToken cancellationToken);
    Task<PagedResult<WorkItem>> ListByProjectAsync(
        Guid tenantId,
        Guid projectId,
        ProjectPermission permission,
        int skip,
        int take,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkItem>> ListByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> workItemIds,
        ProjectPermission permission,
        CancellationToken cancellationToken);
}

public interface ITenantOwnerLock
{
    Task AcquireAsync(Guid tenantId, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
