using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Identity;
using Orbit.Domain.Projects;
using Orbit.Domain.Settings;
using Orbit.Domain.Workspaces;

namespace Orbit.Infrastructure.Persistence;

internal sealed class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> builder)
    {
        builder.ToTable("user_preferences");
        builder.HasKey(preference => preference.UserId);
        builder.Property(preference => preference.UserId).HasColumnName("user_id").ValueGeneratedNever();
        builder.Property(preference => preference.Locale).HasColumnName("locale").HasMaxLength(35).IsRequired();
        builder.Property(preference => preference.TimeZone).HasColumnName("time_zone").HasMaxLength(100).IsRequired();
        builder.Property(preference => preference.Theme).HasColumnName("theme").HasConversion<string>().HasMaxLength(16);
        builder.Property(preference => preference.Density).HasColumnName("density").HasConversion<string>().HasMaxLength(16);
        builder.Property(preference => preference.ReduceMotion).HasColumnName("reduce_motion");
        builder.Property(preference => preference.HighContrast).HasColumnName("high_contrast");
        builder.Property(preference => preference.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(preference => preference.UpdatedAt).HasColumnName("updated_at");
        builder.HasOne<UserAccount>().WithOne().HasForeignKey<UserPreference>(preference => preference.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences");
        builder.HasKey(preference => preference.UserId);
        builder.Property(preference => preference.UserId).HasColumnName("user_id").ValueGeneratedNever();
        builder.Property(preference => preference.InAppEnabled).HasColumnName("in_app_enabled");
        builder.Property(preference => preference.EmailEnabled).HasColumnName("email_enabled");
        builder.Property(preference => preference.DigestCadence).HasColumnName("digest_cadence").HasConversion<string>().HasMaxLength(16);
        builder.Property(preference => preference.QuietHoursStart).HasColumnName("quiet_hours_start");
        builder.Property(preference => preference.QuietHoursEnd).HasColumnName("quiet_hours_end");
        builder.Property(preference => preference.SelfNotify).HasColumnName("self_notify");
        builder.Property(preference => preference.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(preference => preference.UpdatedAt).HasColumnName("updated_at");
        builder.HasOne<UserAccount>().WithOne().HasForeignKey<NotificationPreference>(preference => preference.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class WorkspaceSettingConfiguration : IEntityTypeConfiguration<WorkspaceSetting>
{
    public void Configure(EntityTypeBuilder<WorkspaceSetting> builder)
    {
        builder.ToTable("workspace_settings");
        builder.HasKey(setting => setting.TenantId);
        builder.Property(setting => setting.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        builder.Property(setting => setting.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(setting => setting.DefaultLocale).HasColumnName("default_locale").HasMaxLength(35).IsRequired();
        builder.Property(setting => setting.DefaultTimeZone).HasColumnName("default_time_zone").HasMaxLength(100).IsRequired();
        builder.Property(setting => setting.AllowMemberProjectCreation).HasColumnName("allow_member_project_creation");
        builder.Property(setting => setting.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(setting => setting.UpdatedAt).HasColumnName("updated_at");
        builder.HasOne<Workspace>().WithOne().HasForeignKey<WorkspaceSetting>(setting => setting.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProjectSettingConfiguration : IEntityTypeConfiguration<ProjectSetting>
{
    public void Configure(EntityTypeBuilder<ProjectSetting> builder)
    {
        builder.ToTable("project_settings");
        builder.HasKey(setting => new { setting.TenantId, setting.ProjectId });
        builder.Property(setting => setting.TenantId).HasColumnName("tenant_id");
        builder.Property(setting => setting.ProjectId).HasColumnName("project_id");
        builder.Property(setting => setting.DefaultWorkItemType).HasColumnName("default_work_item_type").HasConversion<string>().HasMaxLength(32);
        builder.Property(setting => setting.DefaultPriority).HasColumnName("default_priority").HasConversion<string>().HasMaxLength(16);
        builder.Property(setting => setting.EnableReleases).HasColumnName("enable_releases");
        builder.Property(setting => setting.EnableTimeTracking).HasColumnName("enable_time_tracking");
        builder.Property(setting => setting.RepositoryUrl).HasColumnName("repository_url").HasMaxLength(2048);
        builder.Property(setting => setting.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(setting => setting.UpdatedAt).HasColumnName("updated_at");
        builder.HasOne<Project>().WithOne().HasForeignKey<ProjectSetting>(setting => new { setting.TenantId, setting.ProjectId })
            .HasPrincipalKey<Project>(project => new { project.TenantId, project.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
