using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Identity;
using Orbit.Domain.Settings;
using Orbit.Domain.Workspaces;

namespace Orbit.Infrastructure.Persistence;

internal sealed class SettingsRepository(OrbitDbContext dbContext) : ISettingsRepository
{
    public Task<UserAccount?> GetUserAccountAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.UserAccounts.SingleOrDefaultAsync(account => account.Id == userId, cancellationToken);

    public async Task<IReadOnlyList<UserAccount>> GetUserAccountsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        return await dbContext.UserAccounts
            .Where(account => userIds.Contains(account.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<UserPreference?> GetUserPreferenceAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.UserPreferences.SingleOrDefaultAsync(preference => preference.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<UserPreference>> GetUserPreferencesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        return await dbContext.UserPreferences
            .Where(preference => userIds.Contains(preference.UserId))
            .ToListAsync(cancellationToken);
    }

    public Task<NotificationPreference?> GetNotificationPreferenceAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.NotificationPreferences.SingleOrDefaultAsync(
            preference => preference.UserId == userId,
            cancellationToken);

    public async Task<IReadOnlyList<NotificationPreference>> GetNotificationPreferencesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        return await dbContext.NotificationPreferences
            .Where(preference => userIds.Contains(preference.UserId))
            .ToListAsync(cancellationToken);
    }

    public Task<Workspace?> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.Workspaces.SingleOrDefaultAsync(workspace => workspace.Id == tenantId, cancellationToken);

    public Task<WorkspaceSetting?> GetWorkspaceSettingAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.WorkspaceSettings.SingleOrDefaultAsync(setting => setting.TenantId == tenantId, cancellationToken);

    public Task<WorkspaceTypographySetting?> GetWorkspaceTypographySettingAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        dbContext.WorkspaceTypographySettings.SingleOrDefaultAsync(
            setting => setting.TenantId == tenantId,
            cancellationToken);

    public Task<ProjectSetting?> GetProjectSettingAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken) =>
        dbContext.ProjectSettings.SingleOrDefaultAsync(
            setting => setting.TenantId == tenantId && setting.ProjectId == projectId,
            cancellationToken);

    public async Task AddUserPreferenceAsync(UserPreference preference, CancellationToken cancellationToken) =>
        await dbContext.UserPreferences.AddAsync(preference, cancellationToken);

    public async Task AddNotificationPreferenceAsync(
        NotificationPreference preference,
        CancellationToken cancellationToken) =>
        await dbContext.NotificationPreferences.AddAsync(preference, cancellationToken);

    public async Task AddWorkspaceSettingAsync(WorkspaceSetting setting, CancellationToken cancellationToken) =>
        await dbContext.WorkspaceSettings.AddAsync(setting, cancellationToken);

    public async Task AddWorkspaceTypographySettingAsync(
        WorkspaceTypographySetting setting,
        CancellationToken cancellationToken) =>
        await dbContext.WorkspaceTypographySettings.AddAsync(setting, cancellationToken);

    public async Task AddProjectSettingAsync(ProjectSetting setting, CancellationToken cancellationToken) =>
        await dbContext.ProjectSettings.AddAsync(setting, cancellationToken);
}
