using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Projects;

namespace Orbit.Infrastructure.Persistence;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects", table =>
        {
            table.HasCheckConstraint("ck_projects_key", "key ~ '^[A-Z0-9]{2,10}$'");
            table.HasCheckConstraint("ck_projects_next_sequence", "next_item_sequence > 0");
        });
        builder.HasKey(project => project.Id);
        builder.HasAlternateKey(project => new { project.TenantId, project.Id });
        builder.Property(project => project.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(project => project.TenantId).HasColumnName("tenant_id");
        builder.Property(project => project.Key).HasColumnName("key").HasMaxLength(10).IsRequired();
        builder.Property(project => project.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(project => project.NextItemSequence).HasColumnName("next_item_sequence");
        builder.Property(project => project.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(project => project.CreatedAt).HasColumnName("created_at");
        builder.Property(project => project.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(project => new { project.TenantId, project.Key }).IsUnique();
    }
}
