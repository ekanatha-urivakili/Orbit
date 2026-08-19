using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.WorkItems;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemWorklogConfiguration : IEntityTypeConfiguration<WorkItemWorklog>
{
    public void Configure(EntityTypeBuilder<WorkItemWorklog> builder)
    {
        builder.ToTable("work_item_worklogs");

        builder.HasKey(worklog => worklog.Id);
        builder.Property(worklog => worklog.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(worklog => worklog.TenantId).HasColumnName("tenant_id");
        builder.Property(worklog => worklog.WorkItemId).HasColumnName("work_item_id");
        builder.Property(worklog => worklog.AuthorMembershipId).HasColumnName("author_membership_id");
        builder.Property(worklog => worklog.MinutesSpent).HasColumnName("minutes_spent");
        builder.Property(worklog => worklog.WorkDate).HasColumnName("work_date").HasColumnType("date");
        builder.Property(worklog => worklog.Description).HasColumnName("description").HasMaxLength(2_000);
        builder.Property(worklog => worklog.CreatedAt).HasColumnName("created_at");

        builder.HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(worklog => new { worklog.TenantId, worklog.WorkItemId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(worklog => new { worklog.TenantId, worklog.WorkItemId });
    }
}
