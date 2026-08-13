using Orbit.Domain.Choices;
using Orbit.Domain.Common;

namespace Orbit.Domain.Settings;

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public enum DensityPreference
{
    Comfortable,
    Compact
}

public enum DigestCadence
{
    None,
    Daily,
    Weekly
}

public sealed class UserPreference
{
    private UserPreference()
    {
    }

    private UserPreference(Guid userId, DateTimeOffset now)
    {
        UserId = userId;
        Locale = "en-GB";
        TimeZone = "Europe/London";
        Theme = ThemePreference.System;
        Density = DensityPreference.Comfortable;
        Version = 1;
        UpdatedAt = now;
    }

    public Guid UserId { get; private set; }
    public string Locale { get; private set; } = string.Empty;
    public string TimeZone { get; private set; } = string.Empty;
    public ThemePreference Theme { get; private set; }
    public DensityPreference Density { get; private set; }
    public bool ReduceMotion { get; private set; }
    public bool HighContrast { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static UserPreference Create(Guid userId, DateTimeOffset now) =>
        userId == Guid.Empty
            ? throw new DomainException("User id is required.")
            : new UserPreference(userId, now);

    public void Update(
        string locale,
        string timeZone,
        ThemePreference theme,
        DensityPreference density,
        bool reduceMotion,
        bool highContrast,
        DateTimeOffset now)
    {
        var normalizedLocale = locale.Trim();
        var normalizedTimeZone = timeZone.Trim();
        if (normalizedLocale.Length is < 2 or > 35)
        {
            throw new DomainException("Locale must contain 2 to 35 characters.");
        }

        if (normalizedTimeZone.Length is < 3 or > 100)
        {
            throw new DomainException("Time zone must contain 3 to 100 characters.");
        }

        Locale = normalizedLocale;
        TimeZone = normalizedTimeZone;
        Theme = theme;
        Density = density;
        ReduceMotion = reduceMotion;
        HighContrast = highContrast;
        Version++;
        UpdatedAt = now;
    }
}

public sealed class NotificationPreference
{
    private NotificationPreference()
    {
    }

    private NotificationPreference(Guid userId, DateTimeOffset now)
    {
        UserId = userId;
        InAppEnabled = true;
        EmailEnabled = true;
        DigestCadence = DigestCadence.Daily;
        Version = 1;
        UpdatedAt = now;
    }

    public Guid UserId { get; private set; }
    public bool InAppEnabled { get; private set; }
    public bool EmailEnabled { get; private set; }
    public DigestCadence DigestCadence { get; private set; }
    public TimeOnly? QuietHoursStart { get; private set; }
    public TimeOnly? QuietHoursEnd { get; private set; }
    public bool SelfNotify { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static NotificationPreference Create(Guid userId, DateTimeOffset now) =>
        userId == Guid.Empty
            ? throw new DomainException("User id is required.")
            : new NotificationPreference(userId, now);

    public void Update(
        bool inAppEnabled,
        bool emailEnabled,
        DigestCadence digestCadence,
        TimeOnly? quietHoursStart,
        TimeOnly? quietHoursEnd,
        bool selfNotify,
        DateTimeOffset now)
    {
        if ((quietHoursStart is null) != (quietHoursEnd is null))
        {
            throw new DomainException("Quiet hours require both a start and an end time.");
        }

        InAppEnabled = inAppEnabled;
        EmailEnabled = emailEnabled;
        DigestCadence = digestCadence;
        QuietHoursStart = quietHoursStart;
        QuietHoursEnd = quietHoursEnd;
        SelfNotify = selfNotify;
        Version++;
        UpdatedAt = now;
    }
}

public sealed class WorkspaceSetting
{
    private WorkspaceSetting()
    {
    }

    private WorkspaceSetting(Guid tenantId, DateTimeOffset now)
    {
        TenantId = tenantId;
        DefaultLocale = "en-GB";
        DefaultTimeZone = "Europe/London";
        Version = 1;
        UpdatedAt = now;
    }

    public Guid TenantId { get; private set; }
    public string? Description { get; private set; }
    public string DefaultLocale { get; private set; } = string.Empty;
    public string DefaultTimeZone { get; private set; } = string.Empty;
    public bool AllowMemberProjectCreation { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static WorkspaceSetting Create(Guid tenantId, DateTimeOffset now) =>
        tenantId == Guid.Empty
            ? throw new DomainException("Tenant id is required.")
            : new WorkspaceSetting(tenantId, now);

    public void Update(
        string? description,
        string defaultLocale,
        string defaultTimeZone,
        bool allowMemberProjectCreation,
        DateTimeOffset now)
    {
        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        var normalizedLocale = defaultLocale.Trim();
        var normalizedTimeZone = defaultTimeZone.Trim();
        if (normalizedDescription?.Length > 1000)
        {
            throw new DomainException("Workspace description cannot exceed 1,000 characters.");
        }

        if (normalizedLocale.Length is < 2 or > 35 || normalizedTimeZone.Length is < 3 or > 100)
        {
            throw new DomainException("Workspace locale or time zone is invalid.");
        }

        Description = normalizedDescription;
        DefaultLocale = normalizedLocale;
        DefaultTimeZone = normalizedTimeZone;
        AllowMemberProjectCreation = allowMemberProjectCreation;
        Version++;
        UpdatedAt = now;
    }
}

public sealed class ProjectSetting
{
    private ProjectSetting()
    {
    }

    private ProjectSetting(Guid tenantId, Guid projectId, DateTimeOffset now)
    {
        TenantId = tenantId;
        ProjectId = projectId;
        DefaultWorkItemType = WorkItemType.Task;
        DefaultPriority = Priority.Medium;
        Version = 1;
        UpdatedAt = now;
    }

    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public WorkItemType DefaultWorkItemType { get; private set; }
    public Priority DefaultPriority { get; private set; }
    public bool EnableReleases { get; private set; } = true;
    public bool EnableTimeTracking { get; private set; } = true;
    public string? RepositoryUrl { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static ProjectSetting Create(Guid tenantId, Guid projectId, DateTimeOffset now) =>
        tenantId == Guid.Empty || projectId == Guid.Empty
            ? throw new DomainException("Tenant and project ids are required.")
            : new ProjectSetting(tenantId, projectId, now);

    public void Update(
        WorkItemType defaultWorkItemType,
        Priority defaultPriority,
        bool enableReleases,
        bool enableTimeTracking,
        string? repositoryUrl,
        DateTimeOffset now)
    {
        if (defaultWorkItemType == WorkItemType.Subtask)
        {
            throw new DomainException("Subtask cannot be the default work item type.");
        }

        var normalizedRepositoryUrl = string.IsNullOrWhiteSpace(repositoryUrl) ? null : repositoryUrl.Trim();
        if (normalizedRepositoryUrl?.Length > 2048)
        {
            throw new DomainException("Repository URL cannot exceed 2,048 characters.");
        }

        DefaultWorkItemType = defaultWorkItemType;
        DefaultPriority = defaultPriority;
        EnableReleases = enableReleases;
        EnableTimeTracking = enableTimeTracking;
        RepositoryUrl = normalizedRepositoryUrl;
        Version++;
        UpdatedAt = now;
    }
}
