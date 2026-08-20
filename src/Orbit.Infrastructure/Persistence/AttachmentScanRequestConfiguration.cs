using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Messaging;

namespace Orbit.Infrastructure.Persistence;

internal sealed class AttachmentScanRequestConfiguration : IEntityTypeConfiguration<AttachmentScanRequest>
{
    public void Configure(EntityTypeBuilder<AttachmentScanRequest> builder)
    {
        builder.ToTable("attachment_scan_requests");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(request => request.TenantId).HasColumnName("tenant_id");
        builder.Property(request => request.WorkItemId).HasColumnName("work_item_id");
        builder.Property(request => request.AttachmentId).HasColumnName("attachment_id");
        builder.Property(request => request.ObjectKey).HasColumnName("object_key").HasMaxLength(1024).IsRequired();
        builder.Property(request => request.CreatedAt).HasColumnName("created_at");
        builder.Property(request => request.ProcessedAt).HasColumnName("processed_at");
        builder.Property(request => request.Attempts).HasColumnName("attempts");
        builder.Property(request => request.LastError).HasColumnName("last_error").HasMaxLength(2048);

        // Claim-batch query: pending, under the attempt cap, oldest first.
        builder.HasIndex(request => new { request.ProcessedAt, request.Attempts, request.CreatedAt });
    }
}
