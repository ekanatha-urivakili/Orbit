using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.WorkItems;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemCommentConfiguration : IEntityTypeConfiguration<WorkItemComment>
{
    public void Configure(EntityTypeBuilder<WorkItemComment> builder)
    {
        builder.ToTable("work_item_comments", table =>
        {
            table.HasCheckConstraint("ck_work_item_comments_version", "version > 0");
            table.HasCheckConstraint(
                "ck_work_item_comments_body_length",
                "char_length(body) BETWEEN 1 AND 10000");
        });

        builder.HasKey(comment => comment.Id);
        builder.Property(comment => comment.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(comment => comment.TenantId).HasColumnName("tenant_id");
        builder.Property(comment => comment.WorkItemId).HasColumnName("work_item_id");
        builder.Property(comment => comment.AuthorMembershipId).HasColumnName("author_membership_id");
        builder.Property(comment => comment.Body).HasColumnName("body").HasMaxLength(10_000).IsRequired();
        builder.Property(comment => comment.MentionedUserIds)
            .HasColumnName("mentioned_user_ids")
            .HasColumnType("uuid[]");
        builder.Property(comment => comment.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(comment => comment.CreatedAt).HasColumnName("created_at");
        builder.Property(comment => comment.UpdatedAt).HasColumnName("updated_at");
        builder.Property(comment => comment.LastEditedAt).HasColumnName("last_edited_at");
        builder.Property(comment => comment.DeletedAt).HasColumnName("deleted_at");

        // Composite FK guards tenant isolation — a comment cannot reference a work item in another tenant.
        builder.HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(comment => new { comment.TenantId, comment.WorkItemId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Cascade);

        // Primary list query: all comments for a work item in chronological order.
        builder.HasIndex(comment => new { comment.TenantId, comment.WorkItemId, comment.CreatedAt })
            .HasDatabaseName("ix_work_item_comments_tenant_item_created");

        // Point lookup for edit/delete.
        builder.HasIndex(comment => new { comment.TenantId, comment.WorkItemId, comment.Id })
            .HasDatabaseName("ix_work_item_comments_tenant_item_id");
    }
}
