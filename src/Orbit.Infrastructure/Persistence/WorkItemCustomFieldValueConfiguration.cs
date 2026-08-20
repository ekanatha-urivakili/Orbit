using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Configuration;
using Orbit.Domain.WorkItems;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemCustomFieldValueConfiguration : IEntityTypeConfiguration<WorkItemCustomFieldValue>
{
    public void Configure(EntityTypeBuilder<WorkItemCustomFieldValue> builder)
    {
        builder.ToTable("work_item_custom_field_values");

        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.TenantId).HasColumnName("tenant_id");
        builder.Property(value => value.WorkItemId).HasColumnName("work_item_id");
        builder.Property(value => value.CustomFieldDefinitionId).HasColumnName("custom_field_definition_id");
        builder.Property(value => value.Values).HasColumnName("values").HasColumnType("text[]");
        builder.Property(value => value.CreatedAt).HasColumnName("created_at");
        builder.Property(value => value.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(value => new { value.TenantId, value.WorkItemId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<CustomFieldDefinition>()
            .WithMany()
            .HasForeignKey(value => new { value.TenantId, value.CustomFieldDefinitionId })
            .HasPrincipalKey(definition => new { definition.TenantId, definition.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(value => new { value.TenantId, value.WorkItemId, value.CustomFieldDefinitionId })
            .IsUnique()
            .HasDatabaseName("ux_work_item_custom_field_values_tenant_item_field");
    }
}
