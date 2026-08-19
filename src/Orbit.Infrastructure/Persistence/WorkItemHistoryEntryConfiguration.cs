using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.WorkItems;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemHistoryEntryConfiguration : IEntityTypeConfiguration<WorkItemHistoryEntry>
{
    public void Configure(EntityTypeBuilder<WorkItemHistoryEntry> builder)
    {
        builder.ToTable("work_item_history_entries");

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entry => entry.TenantId).HasColumnName("tenant_id");
        builder.Property(entry => entry.WorkItemId).HasColumnName("work_item_id");
        builder.Property(entry => entry.ChangedByMembershipId).HasColumnName("changed_by_membership_id");
        builder.Property(entry => entry.FieldName).HasColumnName("field_name").HasMaxLength(255).IsRequired();
        builder.Property(entry => entry.OldValue).HasColumnName("old_value").HasMaxLength(4_000);
        builder.Property(entry => entry.NewValue).HasColumnName("new_value").HasMaxLength(4_000);
        builder.Property(entry => entry.ChangedAt).HasColumnName("changed_at");

        // Composite FK guards tenant isolation — an entry cannot reference a work item in another tenant.
        builder.HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(entry => new { entry.TenantId, entry.WorkItemId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Cascade);

        // Primary list query: all history entries for a work item in chronological order.
        builder.HasIndex(entry => new { entry.TenantId, entry.WorkItemId, entry.ChangedAt })
            .HasDatabaseName("ix_work_item_history_entries_tenant_item_changed");
    }
}
