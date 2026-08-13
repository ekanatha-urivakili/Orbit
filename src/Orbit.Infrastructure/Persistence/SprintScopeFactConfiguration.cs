using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Boards;

namespace Orbit.Infrastructure.Persistence;

internal sealed class SprintScopeFactConfiguration : IEntityTypeConfiguration<SprintScopeFact>
{
    public void Configure(EntityTypeBuilder<SprintScopeFact> builder)
    {
        builder.ToTable("sprint_scope_facts");
        builder.HasKey(fact => fact.Id);
        builder.Property(fact => fact.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(fact => fact.TenantId).HasColumnName("tenant_id");
        builder.Property(fact => fact.SprintId).HasColumnName("sprint_id");
        builder.Property(fact => fact.WorkItemId).HasColumnName("work_item_id");
        builder.Property(fact => fact.FactType).HasColumnName("fact_type").HasConversion<string>().HasMaxLength(32);
        builder.Property(fact => fact.EstimateDelta).HasColumnName("estimate_delta").HasColumnType("numeric");
        builder.Property(fact => fact.OccurredAt).HasColumnName("occurred_at");
        builder.Property(fact => fact.RecordedAt).HasColumnName("recorded_at");
        builder.HasIndex(fact => new { fact.TenantId, fact.SprintId, fact.OccurredAt, fact.Id });
        builder.HasOne<Sprint>()
            .WithMany()
            .HasForeignKey(fact => new { fact.TenantId, fact.SprintId })
            .HasPrincipalKey(sprint => new { sprint.TenantId, sprint.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
