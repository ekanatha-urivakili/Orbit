using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Configuration;
using Orbit.Domain.Projects;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemStatusDefinitionConfiguration : IEntityTypeConfiguration<WorkItemStatusDefinition>
{
    public void Configure(EntityTypeBuilder<WorkItemStatusDefinition> builder)
    {
        builder.ToTable("work_item_status_definitions", table =>
        {
            table.HasCheckConstraint("ck_work_item_status_definitions_order", "\"order\" BETWEEN 0 AND 100000");
            table.HasCheckConstraint("ck_work_item_status_definitions_version", "version > 0");
        });
        builder.HasKey(definition => definition.Id);
        builder.Property(definition => definition.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(definition => definition.TenantId).HasColumnName("tenant_id");
        builder.Property(definition => definition.ProjectId).HasColumnName("project_id");
        builder.Property(definition => definition.Key).HasColumnName("key").HasMaxLength(64).IsRequired();
        builder.Property(definition => definition.Name).HasColumnName("name").HasMaxLength(60).IsRequired();
        builder.Property(definition => definition.Category).HasColumnName("category").HasConversion<string>().HasMaxLength(16);
        builder.Property(definition => definition.Order).HasColumnName("order");
        builder.Property(definition => definition.ColorToken).HasColumnName("color_token").HasMaxLength(32).IsRequired();
        builder.Property(definition => definition.IsSystem).HasColumnName("is_system");
        builder.Property(definition => definition.IsDefault).HasColumnName("is_default");
        builder.Property(definition => definition.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(definition => definition.CreatedAt).HasColumnName("created_at");
        builder.Property(definition => definition.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(definition => new { definition.TenantId, definition.ProjectId, definition.Key }).IsUnique();
        builder.HasIndex(definition => new { definition.TenantId, definition.ProjectId, definition.Order });
        // Defense in depth alongside the handler that flips IsDefault: at most one default status per project.
        builder.HasIndex(definition => new { definition.TenantId, definition.ProjectId })
            .IsUnique()
            .HasFilter("is_default = true")
            .HasDatabaseName("ux_work_item_status_definitions_one_default_per_project");
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(definition => new { definition.TenantId, definition.ProjectId })
            .HasPrincipalKey(project => new { project.TenantId, project.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
