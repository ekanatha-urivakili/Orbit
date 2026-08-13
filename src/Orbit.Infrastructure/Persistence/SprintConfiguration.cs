using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Boards;
using Orbit.Domain.Projects;

namespace Orbit.Infrastructure.Persistence;

internal sealed class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    public void Configure(EntityTypeBuilder<Sprint> builder)
    {
        builder.ToTable("sprints");
        builder.HasKey(sprint => sprint.Id);
        builder.Property(sprint => sprint.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(sprint => sprint.TenantId).HasColumnName("tenant_id");
        builder.Property(sprint => sprint.ProjectId).HasColumnName("project_id");
        builder.Property(sprint => sprint.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(sprint => sprint.Goal).HasColumnName("goal").HasMaxLength(2_000);
        builder.Property(sprint => sprint.State).HasColumnName("state").HasConversion<string>().HasMaxLength(16);
        builder.Property(sprint => sprint.StartDate).HasColumnName("start_date");
        builder.Property(sprint => sprint.EndDate).HasColumnName("end_date");
        builder.Property(sprint => sprint.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(sprint => sprint.CreatedAt).HasColumnName("created_at");
        builder.Property(sprint => sprint.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(sprint => new { sprint.TenantId, sprint.ProjectId });
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(sprint => new { sprint.TenantId, sprint.ProjectId })
            .HasPrincipalKey(project => new { project.TenantId, project.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
