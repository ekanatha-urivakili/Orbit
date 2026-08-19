using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.WorkItems;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemWatcherConfiguration : IEntityTypeConfiguration<WorkItemWatcher>
{
    public void Configure(EntityTypeBuilder<WorkItemWatcher> builder)
    {
        builder.ToTable("work_item_watchers");

        builder.HasKey(watcher => watcher.Id);
        builder.Property(watcher => watcher.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(watcher => watcher.TenantId).HasColumnName("tenant_id");
        builder.Property(watcher => watcher.WorkItemId).HasColumnName("work_item_id");
        builder.Property(watcher => watcher.UserId).HasColumnName("user_id");
        builder.Property(watcher => watcher.CreatedAt).HasColumnName("created_at");

        // Composite FK guards tenant isolation — a watcher cannot reference a work item in another tenant.
        builder.HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(watcher => new { watcher.TenantId, watcher.WorkItemId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Cascade);

        // One watch row per (work item, user); also serves the point lookup for watch/unwatch.
        builder.HasIndex(watcher => new { watcher.TenantId, watcher.WorkItemId, watcher.UserId })
            .IsUnique()
            .HasDatabaseName("ux_work_item_watchers_tenant_item_user");
    }
}
