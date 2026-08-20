using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Idempotency;

namespace Orbit.Infrastructure.Persistence;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(record => record.TenantId).HasColumnName("tenant_id");
        builder.Property(record => record.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(255);
        builder.Property(record => record.RequestPath).HasColumnName("request_path").HasMaxLength(2048);
        builder.Property(record => record.ResponseStatusCode).HasColumnName("response_status_code");
        builder.Property(record => record.ResponseBody).HasColumnName("response_body");
        builder.Property(record => record.ResponseContentType).HasColumnName("response_content_type").HasMaxLength(255);
        builder.Property(record => record.CreatedAt).HasColumnName("created_at");
        builder.Property(record => record.ExpiresAt).HasColumnName("expires_at");
        builder.Property(record => record.CompletedAt).HasColumnName("completed_at");

        // Reservation/replay lookup key, and the conflict target for the ON CONFLICT upsert in
        // IdempotencyRecordRepository.
        builder.HasIndex(record => new { record.TenantId, record.IdempotencyKey, record.RequestPath })
            .IsUnique()
            .HasDatabaseName("ux_idempotency_records_tenant_key_path");
    }
}
