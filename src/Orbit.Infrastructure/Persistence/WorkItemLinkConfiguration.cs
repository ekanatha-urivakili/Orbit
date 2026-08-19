using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.WorkItems;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemLinkConfiguration : IEntityTypeConfiguration<WorkItemLink>
{
    public void Configure(EntityTypeBuilder<WorkItemLink> builder)
    {
        builder.ToTable("work_item_links", table =>
        {
            table.HasCheckConstraint("ck_work_item_links_kind", "kind IN ('Blocks', 'RelatesTo', 'Duplicates')");
            table.HasCheckConstraint("ck_work_item_links_distinct", "source_work_item_id <> target_work_item_id");
        });
        builder.HasKey(link => link.Id);
        builder.Property(link => link.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(link => link.TenantId).HasColumnName("tenant_id");
        builder.Property(link => link.SourceWorkItemId).HasColumnName("source_work_item_id");
        builder.Property(link => link.TargetWorkItemId).HasColumnName("target_work_item_id");
        builder.Property(link => link.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(32);
        builder.Property(link => link.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(link => new { link.TenantId, link.SourceWorkItemId });
        builder.HasIndex(link => new { link.TenantId, link.TargetWorkItemId });
        builder.HasIndex(link => new { link.TenantId, link.SourceWorkItemId, link.TargetWorkItemId, link.Kind }).IsUnique();
        builder.HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(link => new { link.TenantId, link.SourceWorkItemId })
            .HasPrincipalKey(workItem => new { workItem.TenantId, workItem.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(link => new { link.TenantId, link.TargetWorkItemId })
            .HasPrincipalKey(workItem => new { workItem.TenantId, workItem.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
