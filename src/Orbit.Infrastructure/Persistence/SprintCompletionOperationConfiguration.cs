using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Boards;

namespace Orbit.Infrastructure.Persistence;

internal sealed class SprintCompletionOperationConfiguration : IEntityTypeConfiguration<SprintCompletionOperation>
{
    public void Configure(EntityTypeBuilder<SprintCompletionOperation> builder)
    {
        builder.ToTable("sprint_completion_operations");
        builder.HasKey(operation => operation.Id);
        builder.Property(operation => operation.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(operation => operation.TenantId).HasColumnName("tenant_id");
        builder.Property(operation => operation.SprintId).HasColumnName("sprint_id");
        builder.Property(operation => operation.RolloverTargetSprintId).HasColumnName("rollover_target_sprint_id");
        builder.Property(operation => operation.State).HasColumnName("state").HasConversion<string>().HasMaxLength(16);
        builder.Property(operation => operation.ProcessedCount).HasColumnName("processed_count");
        builder.Property(operation => operation.TotalCount).HasColumnName("total_count");
        builder.Property(operation => operation.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(operation => new { operation.TenantId, operation.SprintId }).IsUnique();
        builder.HasOne<Sprint>()
            .WithMany()
            .HasForeignKey(operation => new { operation.TenantId, operation.SprintId })
            .HasPrincipalKey(sprint => new { sprint.TenantId, sprint.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
