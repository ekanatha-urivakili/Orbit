using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.WorkItems;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemVoteConfiguration : IEntityTypeConfiguration<WorkItemVote>
{
    public void Configure(EntityTypeBuilder<WorkItemVote> builder)
    {
        builder.ToTable("work_item_votes");

        builder.HasKey(vote => vote.Id);
        builder.Property(vote => vote.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(vote => vote.TenantId).HasColumnName("tenant_id");
        builder.Property(vote => vote.WorkItemId).HasColumnName("work_item_id");
        builder.Property(vote => vote.UserId).HasColumnName("user_id");
        builder.Property(vote => vote.CreatedAt).HasColumnName("created_at");

        builder.HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(vote => new { vote.TenantId, vote.WorkItemId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(vote => new { vote.TenantId, vote.WorkItemId, vote.UserId })
            .IsUnique()
            .HasDatabaseName("ux_work_item_votes_tenant_item_user");
    }
}
