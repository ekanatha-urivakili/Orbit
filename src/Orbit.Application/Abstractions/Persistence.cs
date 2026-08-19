using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Boards;
using Orbit.Domain.Choices;
using Orbit.Domain.Configuration;
using Orbit.Domain.Directory;
using Orbit.Domain.Identity;
using Orbit.Domain.Messaging;
using Orbit.Domain.Organizations;
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
    MembershipTier MembershipTier { get; }
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
    /// <summary>
    /// Returns only the memberships whose IDs are in <paramref name="membershipIds"/>.
    /// Used to avoid loading every tenant member when only a small subset is needed.
    /// </summary>
    Task<IReadOnlyList<TenantMembership>> ListByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> membershipIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the subset of <paramref name="userIds"/> that currently hold an active membership
    /// in the tenant. Used to filter notification recipients so a deactivated member's stale
    /// <see cref="WorkItemWatcher"/> row or a comment mention of them no longer resolves to a sent
    /// email.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListActiveUserIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);
}

public interface ISettingsRepository
{
    Task<UserAccount?> GetUserAccountAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserAccount>> GetUserAccountsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);
    Task<UserPreference?> GetUserPreferenceAsync(Guid userId, CancellationToken cancellationToken);
    Task<NotificationPreference?> GetNotificationPreferenceAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Bulk variant of <see cref="GetNotificationPreferenceAsync"/> for fan-out notification
    /// triggers (comment mentions/watchers, sprint start/complete) that would otherwise issue one
    /// query per recipient.
    /// </summary>
    Task<IReadOnlyList<NotificationPreference>> GetNotificationPreferencesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);
    Task<Workspace?> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<WorkspaceSetting?> GetWorkspaceSettingAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<WorkspaceTypographySetting?> GetWorkspaceTypographySettingAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<ProjectSetting?> GetProjectSettingAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken);
    Task AddUserPreferenceAsync(UserPreference preference, CancellationToken cancellationToken);
    Task AddNotificationPreferenceAsync(NotificationPreference preference, CancellationToken cancellationToken);
    Task AddWorkspaceSettingAsync(WorkspaceSetting setting, CancellationToken cancellationToken);
    Task AddWorkspaceTypographySettingAsync(WorkspaceTypographySetting setting, CancellationToken cancellationToken);
    Task AddProjectSettingAsync(ProjectSetting setting, CancellationToken cancellationToken);
}

public interface IWorkItemTypeRepository
{
    Task<WorkItemTypeDefinition?> GetAsync(
        Guid tenantId,
        WorkItemType id,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkItemTypeDefinition>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken);
}

public interface ICustomFieldRepository
{
    Task AddAsync(CustomFieldDefinition definition, CancellationToken cancellationToken);
    Task<CustomFieldDefinition?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<CustomFieldDefinition?> GetByKeyAsync(Guid tenantId, string key, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomFieldDefinition>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
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

public sealed record VerifiedExternalIdentity(string Issuer, string Subject, string? Email, bool EmailVerified);

public interface IExternalIdentityTokenValidator
{
    Task<VerifiedExternalIdentity> ValidateAsync(string token, CancellationToken cancellationToken);
}

public sealed record VerifiedGoogleIdentity(string Subject, string? Email, bool EmailVerified, string? Name);

/// <summary>
/// Verifies a Google-issued ID token's signature/issuer/audience (separate from
/// <see cref="IExternalIdentityTokenValidator"/>, which validates against a single
/// installation-configured "Authentication:Authority" - Google is always available regardless of
/// whether that generic external-authority setting is configured).
/// </summary>
public interface IGoogleIdTokenValidator
{
    Task<VerifiedGoogleIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken);
}

/// <summary>
/// Drives the server-side (confidential-client) leg of "Sign in with Google": building the
/// authorize-redirect URL and exchanging an authorization code for an ID token via Google's token
/// endpoint using the configured client secret.
/// </summary>
public interface IGoogleOAuthClient
{
    string BuildAuthorizeUrl(string state);
    Task<string> ExchangeCodeForIdTokenAsync(string code, CancellationToken cancellationToken);
}

/// <summary>
/// Signs/verifies the OAuth <c>state</c> parameter as a compact, storage-free token (mode + nonce +
/// expiry + returnUrl), since the callback runs before any session or database row identifies the in-flight
/// request.
/// </summary>
public interface IOAuthStateCodec
{
    string Encode(string mode, DateTimeOffset now, TimeSpan lifetime, string? returnUrl = null);
    bool TryDecode(string state, DateTimeOffset now, out string mode, out string? returnUrl);
}

public interface IAccessTokenIssuer
{
    TimeSpan RefreshTokenLifetime { get; }

    /// <summary>Refresh-token lifetime for a "remember me" login (<see cref="RefreshSession.IsPersistent"/>).</summary>
    TimeSpan PersistentRefreshTokenLifetime { get; }

    /// <summary>
    /// The issuer this instance signs tokens as - the value a service-account membership's
    /// <c>Issuer</c> must match for <c>TenantTransactionMiddleware</c> to accept a token this
    /// issuer minted.
    /// </summary>
    string LocalIssuer { get; }

    AccessToken IssueUserToken(Guid userId, Guid tenantId, Guid sessionId, DateTimeOffset now);

    AccessToken IssueServiceAccountToken(Guid tenantId, string clientId, DateTimeOffset now);
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
    Task<IReadOnlyList<Workspace>> GetWorkspacesAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken cancellationToken);
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
    Task AddPasswordResetTokenAsync(PasswordResetToken token, CancellationToken cancellationToken);
    Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task RevokeActivePasswordResetTokensForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
    Task UpdateLocalCredentialAsync(LocalCredential credential, CancellationToken cancellationToken);
    Task AddServiceAccountCredentialAsync(ServiceAccountCredential credential, CancellationToken cancellationToken);
    Task AddSignInHandoffAsync(GoogleSignInHandoff handoff, CancellationToken cancellationToken);

    /// <summary>Looks up and deletes a handoff row atomically - it is single-use by construction.</summary>
    Task<GoogleSignInHandoff?> ConsumeSignInHandoffAsync(
        string codeHash,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Looks up the currently-active credential for a client id (there may also be older, revoked
    /// rows sharing the same <see cref="ServiceAccountCredential.ClientId"/> from prior rotations).
    /// </summary>
    Task<ServiceAccountCredential?> GetActiveServiceAccountCredentialByClientIdAsync(
        Guid clientId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<ServiceAccountCredential>> ListActiveServiceAccountCredentialsByMembershipAsync(
        Guid membershipId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Looks up a service-account membership before any ambient tenant context exists (the
    /// token-issuance path) by establishing <c>app.tenant_id</c> itself from the already-verified
    /// credential's own <c>TenantId</c>, mirroring <see cref="ListActiveMembershipsByUserAsync"/>'s
    /// pre-auth technique.
    /// </summary>
    Task<TenantMembership?> GetActiveServiceAccountMembershipAsync(
        Guid tenantId,
        Guid membershipId,
        CancellationToken cancellationToken);
}

public interface IOutboxRepository
{
    Task AddAsync(OutboxEmailMessage message, CancellationToken cancellationToken);
}

public interface IWorkspaceInvitationRepository
{
    Task AddAsync(WorkspaceInvitation invitation, CancellationToken cancellationToken);
    Task<WorkspaceInvitation?> GetActiveByEmailAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken);
    Task<WorkspaceInvitation?> GetByTokenHashAsync(
        Guid tenantId,
        string tokenHash,
        CancellationToken cancellationToken);
    Task<WorkspaceInvitation?> GetAsync(Guid tenantId, Guid invitationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkspaceInvitation>> ListAsync(
        Guid tenantId,
        string? emailSearch,
        WorkspaceInvitationStatus? status,
        CancellationToken cancellationToken);
    Task<UserAccount?> GetUserAccountByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<LocalCredential?> GetUserAccountCredentialAsync(Guid userId, CancellationToken cancellationToken);
    Task<TenantMembership?> GetMembershipByUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken);
    Task<TeamMembership?> GetTeamMembershipAsync(
        Guid tenantId,
        Guid teamId,
        Guid membershipId,
        CancellationToken cancellationToken);
    Task AddUserAccountAsync(UserAccount account, CancellationToken cancellationToken);
    Task AddLocalCredentialAsync(LocalCredential credential, CancellationToken cancellationToken);
    Task AddTenantMembershipAsync(TenantMembership membership, CancellationToken cancellationToken);
    Task AddTeamMembershipAsync(TeamMembership membership, CancellationToken cancellationToken);
}

public interface IBootstrapRepository
{
    Task<bool> IsInitializationRequiredAsync(CancellationToken cancellationToken);
    Task<bool> TryInitializeAsync(
        UserAccount account,
        LocalCredential credential,
        SiteRoleAssignment siteRole,
        Organization organization,
        Workspace workspace,
        OrganizationMembership organizationMembership,
        TenantMembership ownerMembership,
        CancellationToken cancellationToken);
}

/// <summary>
/// Backs public self-service registration (<c>POST /register</c>): unlike <see cref="IBootstrapRepository"/>,
/// this runs unboundedly many times (no advisory-lock singleton guard) and never grants
/// <see cref="SiteRole.SuperAdministrator"/> - each call provisions one brand-new, independent
/// organization/workspace/owner, not the one-time installation superadmin.
/// </summary>
public interface ISignUpRepository
{
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task AddAsync(
        UserAccount account,
        LocalCredential credential,
        Organization organization,
        Workspace workspace,
        OrganizationMembership organizationMembership,
        TenantMembership ownerMembership,
        RefreshSession refreshSession,
        CancellationToken cancellationToken);

    /// <summary>
    /// Provisions a new organization/workspace for a user identified purely by an external identity
    /// (no password, so no <see cref="LocalCredential"/>) - the "Sign in with Google" register path.
    /// A <see cref="GoogleSignInHandoff"/> is created in the same transaction rather than a
    /// <see cref="RefreshSession"/> directly, since the OAuth callback that calls this runs as a
    /// full-page browser redirect and cannot itself hand tokens back to the SPA.
    /// </summary>
    Task ProvisionExternalAccountAsync(
        UserAccount account,
        ExternalIdentity identity,
        Organization organization,
        Workspace workspace,
        OrganizationMembership organizationMembership,
        TenantMembership ownerMembership,
        GoogleSignInHandoff handoff,
        CancellationToken cancellationToken);
}

public interface IWorkspaceProvisioningRepository
{
    Task<bool> IsSiteSuperAdministratorAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);
    Task AddAsync(
        Organization organization,
        Workspace workspace,
        OrganizationMembership organizationMembership,
        TenantMembership ownerMembership,
        Guid currentTenantId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Looks up the caller's membership in the organization that owns <paramref name="workspaceTenantId"/>,
    /// used to authorize adding a second workspace to an existing organization (as opposed to
    /// <see cref="AddAsync"/>'s site-super-admin path, which always creates a brand-new organization).
    /// </summary>
    Task<OrganizationMembership?> GetOrganizationMembershipAsync(
        Guid workspaceTenantId, Guid userId, CancellationToken cancellationToken);

    Task AddWorkspaceToOrganizationAsync(
        Workspace workspace,
        TenantMembership ownerMembership,
        Guid currentTenantId,
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
    Task<IReadOnlyList<SprintScopeFact>> ListBySprintAsync(
        Guid tenantId,
        Guid sprintId,
        CancellationToken cancellationToken);
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

    /// <summary>True when at least one work item has <paramref name="parentWorkItemId"/> as its parent.</summary>
    Task<bool> HasChildrenAsync(Guid tenantId, Guid parentWorkItemId, CancellationToken cancellationToken);

    Task RemoveAsync(WorkItem workItem, CancellationToken cancellationToken);
}

public interface IWorkItemLinkRepository
{
    Task AddAsync(WorkItemLink link, CancellationToken cancellationToken);
    Task<WorkItemLink?> GetAsync(Guid tenantId, Guid linkId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns all links where the work item is either the source or the target.
    /// </summary>
    Task<IReadOnlyList<WorkItemLink>> ListByWorkItemAsync(
        Guid tenantId,
        Guid workItemId,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(
        Guid tenantId,
        Guid sourceWorkItemId,
        Guid targetWorkItemId,
        WorkItemLinkKind kind,
        CancellationToken cancellationToken);

    Task RemoveAsync(WorkItemLink link, CancellationToken cancellationToken);
}

public interface IWorkItemCommentRepository
{
    Task AddAsync(WorkItemComment comment, CancellationToken cancellationToken);

    /// <summary>
    /// Returns a single comment by id, scoped to the work item. Returns <c>null</c> when the
    /// comment does not exist, belongs to a different work item, or the caller's tenant does
    /// not match. The work-item visibility check is the caller's responsibility.
    /// </summary>
    Task<WorkItemComment?> GetAsync(
        Guid tenantId,
        Guid workItemId,
        Guid commentId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns all comments for a work item ordered by <c>CreatedAt ASC</c>, including
    /// soft-deleted stubs. The caller must have verified work-item visibility.
    /// </summary>
    Task<IReadOnlyList<WorkItemComment>> ListByWorkItemAsync(
        Guid tenantId,
        Guid workItemId,
        CancellationToken cancellationToken);
}

public interface IAttachmentRepository
{
    Task AddAsync(Attachment attachment, CancellationToken cancellationToken);

    /// <summary>
    /// Returns a single attachment by id, scoped to the work item. Returns <c>null</c> when it
    /// does not exist, belongs to a different work item, or the caller's tenant does not match.
    /// </summary>
    Task<Attachment?> GetAsync(
        Guid tenantId,
        Guid workItemId,
        Guid attachmentId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns all attachments for a work item ordered by <c>UploadedAt ASC</c>. The caller must
    /// have verified work-item visibility.
    /// </summary>
    Task<IReadOnlyList<Attachment>> ListByWorkItemAsync(
        Guid tenantId,
        Guid workItemId,
        CancellationToken cancellationToken);

    Task RemoveAsync(Attachment attachment, CancellationToken cancellationToken);
}

public interface IWorkItemWatcherRepository
{
    Task AddAsync(WorkItemWatcher watcher, CancellationToken cancellationToken);

    Task<WorkItemWatcher?> GetAsync(
        Guid tenantId, Guid workItemId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkItemWatcher>> ListByWorkItemAsync(
        Guid tenantId, Guid workItemId, CancellationToken cancellationToken);

    Task RemoveAsync(WorkItemWatcher watcher, CancellationToken cancellationToken);
}

public interface IWorkItemVoteRepository
{
    Task AddAsync(WorkItemVote vote, CancellationToken cancellationToken);

    Task<WorkItemVote?> GetAsync(
        Guid tenantId, Guid workItemId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkItemVote>> ListByWorkItemAsync(
        Guid tenantId, Guid workItemId, CancellationToken cancellationToken);

    Task RemoveAsync(WorkItemVote vote, CancellationToken cancellationToken);
}

public interface IWorkItemWorklogRepository
{
    Task AddAsync(WorkItemWorklog worklog, CancellationToken cancellationToken);

    Task<WorkItemWorklog?> GetAsync(Guid tenantId, Guid worklogId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkItemWorklog>> ListByWorkItemAsync(
        Guid tenantId, Guid workItemId, CancellationToken cancellationToken);

    Task RemoveAsync(WorkItemWorklog worklog, CancellationToken cancellationToken);
}

public interface ITenantOwnerLock
{
    Task AcquireAsync(Guid tenantId, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

