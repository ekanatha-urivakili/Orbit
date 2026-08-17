using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.Identity;
using Orbit.Domain.Settings;

namespace Orbit.Application.Settings;

public sealed record ProfileDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    long Version,
    string Locale,
    string TimeZone,
    ThemePreference Theme,
    DensityPreference Density,
    bool ReduceMotion,
    bool HighContrast,
    long PreferenceVersion)
{
    public static ProfileDto From(UserAccount account, UserPreference? preference) =>
        new(
            account.Id,
            account.NormalizedEmail,
            account.DisplayName,
            account.AvatarUrl,
            account.Version,
            preference?.Locale ?? "en-GB",
            preference?.TimeZone ?? "Europe/London",
            preference?.Theme ?? ThemePreference.System,
            preference?.Density ?? DensityPreference.Comfortable,
            preference?.ReduceMotion ?? false,
            preference?.HighContrast ?? false,
            preference?.Version ?? 0);
}

public sealed record NotificationPreferenceDto(
    bool InAppEnabled,
    bool EmailEnabled,
    DigestCadence DigestCadence,
    TimeOnly? QuietHoursStart,
    TimeOnly? QuietHoursEnd,
    bool SelfNotify,
    long Version);

public sealed record WorkspaceSettingDto(
    Guid WorkspaceId,
    string WorkspaceName,
    string? Description,
    string DefaultLocale,
    string DefaultTimeZone,
    bool AllowMemberProjectCreation,
    bool CanAdminister,
    long Version);

public sealed record ProjectSettingDto(
    Guid ProjectId,
    WorkItemType DefaultWorkItemType,
    Priority DefaultPriority,
    bool EnableReleases,
    bool EnableTimeTracking,
    string? RepositoryUrl,
    long Version);

public sealed record TypographySettingDto(
    string LeftFontFamily,
    string LeftFontColor,
    int LeftFontSizePx,
    string MiddleFontFamily,
    string MiddleFontColor,
    int MiddleFontSizePx,
    string RightFontFamily,
    string RightFontColor,
    int RightFontSizePx,
    int ControlHeightPx,
    int ControlFontSizePx,
    bool CanAdminister,
    long Version)
{
    public static TypographySettingDto Default(bool canAdminister) =>
        new(
            WorkspaceTypographySetting.DefaultFontFamily,
            WorkspaceTypographySetting.DefaultInkColor,
            WorkspaceTypographySetting.DefaultFontSizePx,
            WorkspaceTypographySetting.DefaultFontFamily,
            WorkspaceTypographySetting.DefaultInkColor,
            WorkspaceTypographySetting.DefaultFontSizePx,
            WorkspaceTypographySetting.DefaultFontFamily,
            WorkspaceTypographySetting.DefaultInkColor,
            WorkspaceTypographySetting.DefaultFontSizePx,
            WorkspaceTypographySetting.DefaultControlHeightPx,
            WorkspaceTypographySetting.DefaultControlFontSizePx,
            canAdminister,
            0);

    public static TypographySettingDto From(WorkspaceTypographySetting setting, bool canAdminister) =>
        new(
            setting.LeftFontFamily,
            setting.LeftFontColor,
            setting.LeftFontSizePx,
            setting.MiddleFontFamily,
            setting.MiddleFontColor,
            setting.MiddleFontSizePx,
            setting.RightFontFamily,
            setting.RightFontColor,
            setting.RightFontSizePx,
            setting.ControlHeightPx,
            setting.ControlFontSizePx,
            canAdminister,
            setting.Version);
}

internal static class SettingsConcurrency
{
    public static void EnsureVersion(bool exists, long actualVersion, long expectedVersion, string message)
    {
        var matches = exists ? actualVersion == expectedVersion : expectedVersion == 0;
        if (!matches)
        {
            throw new ConcurrencyException(message);
        }
    }
}

public sealed record GetProfileQuery : IQuery<ProfileDto>;

public sealed class GetProfileHandler(
    ICurrentPrincipal principal,
    ISettingsRepository settings) : IRequestHandler<GetProfileQuery, ProfileDto>
{
    public async Task<ProfileDto> Handle(GetProfileQuery request, CancellationToken cancellationToken)
    {
        var userId = PrincipalGuards.RequireUser(principal);
        var account = await settings.GetUserAccountAsync(userId, cancellationToken)
            ?? throw new NotFoundException("User account was not found.");
        var preference = await settings.GetUserPreferenceAsync(userId, cancellationToken);
        return ProfileDto.From(account, preference);
    }
}

public sealed record UpdateProfileCommand(
    string DisplayName,
    string? AvatarUrl,
    long ExpectedVersion) : ICommand<ProfileDto>;

public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(command => command.DisplayName).NotEmpty().Length(2, 120);
        RuleFor(command => command.AvatarUrl).MaximumLength(2048);
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class UpdateProfileHandler(
    ICurrentPrincipal principal,
    ISettingsRepository settings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<UpdateProfileCommand, ProfileDto>
{
    public async Task<ProfileDto> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var userId = PrincipalGuards.RequireUser(principal);
        var account = await settings.GetUserAccountAsync(userId, cancellationToken)
            ?? throw new NotFoundException("User account was not found.");
        if (account.Version != request.ExpectedVersion)
        {
            throw new ConcurrencyException("The profile changed after it was loaded.");
        }

        account.UpdateProfile(request.DisplayName, request.AvatarUrl, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var preference = await settings.GetUserPreferenceAsync(userId, cancellationToken);
        return ProfileDto.From(account, preference);
    }
}

public sealed record UpdateUserPreferenceCommand(
    string Locale,
    string TimeZone,
    ThemePreference Theme,
    DensityPreference Density,
    bool ReduceMotion,
    bool HighContrast,
    long ExpectedVersion) : ICommand<ProfileDto>;

public sealed class UpdateUserPreferenceValidator : AbstractValidator<UpdateUserPreferenceCommand>
{
    public UpdateUserPreferenceValidator()
    {
        RuleFor(command => command.Locale).NotEmpty().Length(2, 35);
        RuleFor(command => command.TimeZone).NotEmpty().Length(3, 100);
        RuleFor(command => command.Theme).IsInEnum();
        RuleFor(command => command.Density).IsInEnum();
        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateUserPreferenceHandler(
    ICurrentPrincipal principal,
    ISettingsRepository settings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<UpdateUserPreferenceCommand, ProfileDto>
{
    public async Task<ProfileDto> Handle(UpdateUserPreferenceCommand request, CancellationToken cancellationToken)
    {
        var userId = PrincipalGuards.RequireUser(principal);
        var account = await settings.GetUserAccountAsync(userId, cancellationToken)
            ?? throw new NotFoundException("User account was not found.");
        var preference = await settings.GetUserPreferenceAsync(userId, cancellationToken);
        SettingsConcurrency.EnsureVersion(
            preference is not null,
            preference?.Version ?? 0,
            request.ExpectedVersion,
            "The preferences changed after they were loaded.");
        if (preference is null)
        {
            preference = UserPreference.Create(userId, timeProvider.GetUtcNow());
            await settings.AddUserPreferenceAsync(preference, cancellationToken);
        }

        preference.Update(
            request.Locale,
            request.TimeZone,
            request.Theme,
            request.Density,
            request.ReduceMotion,
            request.HighContrast,
            timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ProfileDto.From(account, preference);
    }
}

public sealed record GetNotificationPreferenceQuery : IQuery<NotificationPreferenceDto>;

public sealed class GetNotificationPreferenceHandler(
    ICurrentPrincipal principal,
    ISettingsRepository settings) : IRequestHandler<GetNotificationPreferenceQuery, NotificationPreferenceDto>
{
    public async Task<NotificationPreferenceDto> Handle(
        GetNotificationPreferenceQuery request,
        CancellationToken cancellationToken)
    {
        var preference = await settings.GetNotificationPreferenceAsync(
            PrincipalGuards.RequireUser(principal),
            cancellationToken);
        return preference is null
            ? new NotificationPreferenceDto(true, true, DigestCadence.Daily, null, null, false, 0)
            : Map(preference);
    }

    internal static NotificationPreferenceDto Map(NotificationPreference preference) =>
        new(
            preference.InAppEnabled,
            preference.EmailEnabled,
            preference.DigestCadence,
            preference.QuietHoursStart,
            preference.QuietHoursEnd,
            preference.SelfNotify,
            preference.Version);
}

public sealed record UpdateNotificationPreferenceCommand(
    bool InAppEnabled,
    bool EmailEnabled,
    DigestCadence DigestCadence,
    TimeOnly? QuietHoursStart,
    TimeOnly? QuietHoursEnd,
    bool SelfNotify,
    long ExpectedVersion) : ICommand<NotificationPreferenceDto>;

public sealed class UpdateNotificationPreferenceValidator : AbstractValidator<UpdateNotificationPreferenceCommand>
{
    public UpdateNotificationPreferenceValidator()
    {
        RuleFor(command => command.DigestCadence).IsInEnum();
        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateNotificationPreferenceHandler(
    ICurrentPrincipal principal,
    ISettingsRepository settings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<UpdateNotificationPreferenceCommand, NotificationPreferenceDto>
{
    public async Task<NotificationPreferenceDto> Handle(
        UpdateNotificationPreferenceCommand request,
        CancellationToken cancellationToken)
    {
        var userId = PrincipalGuards.RequireUser(principal);
        var preference = await settings.GetNotificationPreferenceAsync(userId, cancellationToken);
        SettingsConcurrency.EnsureVersion(
            preference is not null,
            preference?.Version ?? 0,
            request.ExpectedVersion,
            "The notification preferences changed after they were loaded.");
        if (preference is null)
        {
            preference = NotificationPreference.Create(userId, timeProvider.GetUtcNow());
            await settings.AddNotificationPreferenceAsync(preference, cancellationToken);
        }

        preference.Update(
            request.InAppEnabled,
            request.EmailEnabled,
            request.DigestCadence,
            request.QuietHoursStart,
            request.QuietHoursEnd,
            request.SelfNotify,
            timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return GetNotificationPreferenceHandler.Map(preference);
    }
}

public sealed record GetWorkspaceSettingQuery : IQuery<WorkspaceSettingDto>;

public sealed class GetWorkspaceSettingHandler(
    ITenantContext tenant,
    ICurrentPrincipal principal,
    ISettingsRepository settings) : IRequestHandler<GetWorkspaceSettingQuery, WorkspaceSettingDto>
{
    public async Task<WorkspaceSettingDto> Handle(GetWorkspaceSettingQuery request, CancellationToken cancellationToken)
    {
        var workspace = await settings.GetWorkspaceAsync(tenant.TenantId, cancellationToken)
            ?? throw new NotFoundException("Workspace was not found.");
        var setting = await settings.GetWorkspaceSettingAsync(tenant.TenantId, cancellationToken);
        return new WorkspaceSettingDto(
            workspace.Id,
            workspace.Name,
            setting?.Description,
            setting?.DefaultLocale ?? "en-GB",
            setting?.DefaultTimeZone ?? "Europe/London",
            setting?.AllowMemberProjectCreation ?? false,
            principal.TenantRole is TenantRole.Owner or TenantRole.Administrator,
            setting?.Version ?? 0);
    }
}

public sealed record UpdateWorkspaceSettingCommand(
    string? Description,
    string DefaultLocale,
    string DefaultTimeZone,
    bool AllowMemberProjectCreation,
    long ExpectedVersion) : ICommand<WorkspaceSettingDto>;

public sealed class UpdateWorkspaceSettingHandler(
    ITenantContext tenant,
    ICurrentPrincipal principal,
    ISettingsRepository settings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<UpdateWorkspaceSettingCommand, WorkspaceSettingDto>
{
    public async Task<WorkspaceSettingDto> Handle(
        UpdateWorkspaceSettingCommand request,
        CancellationToken cancellationToken)
    {
        if (principal.TenantRole is not (TenantRole.Owner or TenantRole.Administrator))
        {
            throw new AccessDeniedException("Workspace administration permission is required.");
        }

        var workspace = await settings.GetWorkspaceAsync(tenant.TenantId, cancellationToken)
            ?? throw new NotFoundException("Workspace was not found.");
        var setting = await settings.GetWorkspaceSettingAsync(tenant.TenantId, cancellationToken);
        SettingsConcurrency.EnsureVersion(
            setting is not null,
            setting?.Version ?? 0,
            request.ExpectedVersion,
            "The workspace settings changed after they were loaded.");
        if (setting is null)
        {
            setting = WorkspaceSetting.Create(tenant.TenantId, timeProvider.GetUtcNow());
            await settings.AddWorkspaceSettingAsync(setting, cancellationToken);
        }

        setting.Update(
            request.Description,
            request.DefaultLocale,
            request.DefaultTimeZone,
            request.AllowMemberProjectCreation,
            timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new WorkspaceSettingDto(
            workspace.Id,
            workspace.Name,
            setting.Description,
            setting.DefaultLocale,
            setting.DefaultTimeZone,
            setting.AllowMemberProjectCreation,
            true,
            setting.Version);
    }
}

public sealed record GetTypographySettingQuery : IQuery<TypographySettingDto>;

public sealed class GetTypographySettingHandler(
    ITenantContext tenant,
    ICurrentPrincipal principal,
    ISettingsRepository settings) : IRequestHandler<GetTypographySettingQuery, TypographySettingDto>
{
    public async Task<TypographySettingDto> Handle(
        GetTypographySettingQuery request,
        CancellationToken cancellationToken)
    {
        var canAdminister = principal.TenantRole is TenantRole.Owner or TenantRole.Administrator;
        var setting = await settings.GetWorkspaceTypographySettingAsync(tenant.TenantId, cancellationToken);
        return setting is null
            ? TypographySettingDto.Default(canAdminister)
            : TypographySettingDto.From(setting, canAdminister);
    }
}

public sealed record UpdateTypographySettingCommand(
    string LeftFontFamily,
    string LeftFontColor,
    int LeftFontSizePx,
    string MiddleFontFamily,
    string MiddleFontColor,
    int MiddleFontSizePx,
    string RightFontFamily,
    string RightFontColor,
    int RightFontSizePx,
    int ControlHeightPx,
    int ControlFontSizePx,
    long ExpectedVersion) : ICommand<TypographySettingDto>;

public sealed class UpdateTypographySettingValidator : AbstractValidator<UpdateTypographySettingCommand>
{
    public UpdateTypographySettingValidator()
    {
        RuleFor(command => command.LeftFontFamily).NotEmpty().MaximumLength(200);
        RuleFor(command => command.LeftFontColor).Matches("^#[0-9a-fA-F]{6}$");
        RuleFor(command => command.LeftFontSizePx).InclusiveBetween(10, 24);
        RuleFor(command => command.MiddleFontFamily).NotEmpty().MaximumLength(200);
        RuleFor(command => command.MiddleFontColor).Matches("^#[0-9a-fA-F]{6}$");
        RuleFor(command => command.MiddleFontSizePx).InclusiveBetween(10, 24);
        RuleFor(command => command.RightFontFamily).NotEmpty().MaximumLength(200);
        RuleFor(command => command.RightFontColor).Matches("^#[0-9a-fA-F]{6}$");
        RuleFor(command => command.RightFontSizePx).InclusiveBetween(10, 24);
        RuleFor(command => command.ControlHeightPx).InclusiveBetween(24, 56);
        RuleFor(command => command.ControlFontSizePx).InclusiveBetween(10, 24);
        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateTypographySettingHandler(
    ITenantContext tenant,
    ICurrentPrincipal principal,
    ISettingsRepository settings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<UpdateTypographySettingCommand, TypographySettingDto>
{
    public async Task<TypographySettingDto> Handle(
        UpdateTypographySettingCommand request,
        CancellationToken cancellationToken)
    {
        if (principal.TenantRole is not (TenantRole.Owner or TenantRole.Administrator))
        {
            throw new AccessDeniedException("Workspace administration permission is required.");
        }

        var setting = await settings.GetWorkspaceTypographySettingAsync(tenant.TenantId, cancellationToken);
        SettingsConcurrency.EnsureVersion(
            setting is not null,
            setting?.Version ?? 0,
            request.ExpectedVersion,
            "The typography settings changed after they were loaded.");
        if (setting is null)
        {
            setting = WorkspaceTypographySetting.Create(tenant.TenantId, timeProvider.GetUtcNow());
            await settings.AddWorkspaceTypographySettingAsync(setting, cancellationToken);
        }

        setting.Update(
            request.LeftFontFamily,
            request.LeftFontColor,
            request.LeftFontSizePx,
            request.MiddleFontFamily,
            request.MiddleFontColor,
            request.MiddleFontSizePx,
            request.RightFontFamily,
            request.RightFontColor,
            request.RightFontSizePx,
            request.ControlHeightPx,
            request.ControlFontSizePx,
            timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TypographySettingDto.From(setting, true);
    }
}

public sealed record GetProjectSettingQuery(Guid ProjectId) : IQuery<ProjectSettingDto>;

public sealed class GetProjectSettingHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    ISettingsRepository settings) : IRequestHandler<GetProjectSettingQuery, ProjectSettingDto>
{
    public async Task<ProjectSettingDto> Handle(GetProjectSettingQuery request, CancellationToken cancellationToken)
    {
        _ = await projects.GetAsync(tenant.TenantId, request.ProjectId, ProjectPermission.Administer, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");
        var setting = await settings.GetProjectSettingAsync(tenant.TenantId, request.ProjectId, cancellationToken);
        return setting is null
            ? new ProjectSettingDto(request.ProjectId, WorkItemType.Task, Priority.Medium, true, true, null, 0)
            : Map(setting);
    }

    internal static ProjectSettingDto Map(ProjectSetting setting) =>
        new(
            setting.ProjectId,
            setting.DefaultWorkItemType,
            setting.DefaultPriority,
            setting.EnableReleases,
            setting.EnableTimeTracking,
            setting.RepositoryUrl,
            setting.Version);
}

public sealed record UpdateProjectSettingCommand(
    Guid ProjectId,
    WorkItemType DefaultWorkItemType,
    Priority DefaultPriority,
    bool EnableReleases,
    bool EnableTimeTracking,
    string? RepositoryUrl,
    long ExpectedVersion) : ICommand<ProjectSettingDto>;

public sealed class UpdateProjectSettingHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    ISettingsRepository settings,
    IWorkItemTypeRepository workItemTypes,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<UpdateProjectSettingCommand, ProjectSettingDto>
{
    public async Task<ProjectSettingDto> Handle(
        UpdateProjectSettingCommand request,
        CancellationToken cancellationToken)
    {
        _ = await projects.GetAsync(tenant.TenantId, request.ProjectId, ProjectPermission.Administer, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");
        var itemType = await workItemTypes.GetAsync(
            tenant.TenantId,
            request.DefaultWorkItemType,
            cancellationToken);
        if (itemType is null || !itemType.Enabled)
        {
            throw new ValidationException("The default work item type must be enabled in this workspace.");
        }

        var setting = await settings.GetProjectSettingAsync(tenant.TenantId, request.ProjectId, cancellationToken);
        SettingsConcurrency.EnsureVersion(
            setting is not null,
            setting?.Version ?? 0,
            request.ExpectedVersion,
            "The project settings changed after they were loaded.");
        if (setting is null)
        {
            setting = ProjectSetting.Create(tenant.TenantId, request.ProjectId, timeProvider.GetUtcNow());
            await settings.AddProjectSettingAsync(setting, cancellationToken);
        }

        setting.Update(
            request.DefaultWorkItemType,
            request.DefaultPriority,
            request.EnableReleases,
            request.EnableTimeTracking,
            request.RepositoryUrl,
            timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return GetProjectSettingHandler.Map(setting);
    }
}
