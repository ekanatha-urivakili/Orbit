using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.WorkItems;

namespace Orbit.Infrastructure.Persistence;

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments", table =>
        {
            table.HasCheckConstraint("ck_attachments_size_bytes", "size_bytes > 0 AND size_bytes <= 26214400");
        });

        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(attachment => attachment.TenantId).HasColumnName("tenant_id");
        builder.Property(attachment => attachment.WorkItemId).HasColumnName("work_item_id");
        builder.Property(attachment => attachment.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
        builder.Property(attachment => attachment.ContentType).HasColumnName("content_type").HasMaxLength(255).IsRequired();
        builder.Property(attachment => attachment.SizeBytes).HasColumnName("size_bytes");
        builder.Property(attachment => attachment.ObjectKey).HasColumnName("object_key").HasMaxLength(1024).IsRequired();
        builder.Property(attachment => attachment.UploadedByMembershipId).HasColumnName("uploaded_by_membership_id");
        builder.Property(attachment => attachment.UploadedAt).HasColumnName("uploaded_at");
        builder.Property(attachment => attachment.ScanStatus)
            .HasColumnName("scan_status").HasConversion<string>().HasMaxLength(16).IsRequired()
            .HasDefaultValue(AttachmentScanStatus.Pending);
        builder.Property(attachment => attachment.ScannedAt).HasColumnName("scanned_at");

        // Composite FK guards tenant isolation — an attachment cannot reference a work item in another tenant.
        builder.HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(attachment => new { attachment.TenantId, attachment.WorkItemId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Cascade);

        // Primary list query: all attachments for a work item in upload order.
        builder.HasIndex(attachment => new { attachment.TenantId, attachment.WorkItemId, attachment.UploadedAt })
            .HasDatabaseName("ix_attachments_tenant_item_uploaded");

        // Point lookup for delete.
        builder.HasIndex(attachment => new { attachment.TenantId, attachment.WorkItemId, attachment.Id })
            .HasDatabaseName("ix_attachments_tenant_item_id");

        // Object key is globally unique (it embeds tenant/work-item/random segments already).
        builder.HasIndex(attachment => attachment.ObjectKey).IsUnique().HasDatabaseName("ux_attachments_object_key");
    }
}
