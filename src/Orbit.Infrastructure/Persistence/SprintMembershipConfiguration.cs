using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Boards;
using Orbit.Domain.WorkItems;

namespace Orbit.Infrastructure.Persistence;

internal sealed class SprintMembershipConfiguration : IEntityTypeConfiguration<SprintMembership>
{
    public void Configure(EntityTypeBuilder<SprintMembership> builder)
    {
        builder.ToTable("sprint_memberships");
        builder.HasKey(membership => membership.Id);
        builder.Property(membership => membership.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(membership => membership.TenantId).HasColumnName("tenant_id");
        builder.Property(membership => membership.SprintId).HasColumnName("sprint_id");
        builder.Property(membership => membership.WorkItemId).HasColumnName("work_item_id");
        builder.Property(membership => membership.AddedAt).HasColumnName("added_at");
        builder.Property(membership => membership.RemovedAt).HasColumnName("removed_at");
        builder.HasIndex(membership => new { membership.TenantId, membership.SprintId, membership.RemovedAt });
        builder.HasIndex(membership => new { membership.TenantId, membership.WorkItemId, membership.RemovedAt });
        builder.HasOne<Sprint>()
            .WithMany()
            .HasForeignKey(membership => new { membership.TenantId, membership.SprintId })
            .HasPrincipalKey(sprint => new { sprint.TenantId, sprint.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(membership => new { membership.TenantId, membership.WorkItemId })
            .HasPrincipalKey(workItem => new { workItem.TenantId, workItem.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
