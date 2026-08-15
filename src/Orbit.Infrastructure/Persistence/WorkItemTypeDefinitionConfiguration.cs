using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Configuration;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemTypeDefinitionConfiguration
    : IEntityTypeConfiguration<WorkItemTypeDefinition>
{
    public void Configure(EntityTypeBuilder<WorkItemTypeDefinition> builder)
    {
        builder.ToTable("work_item_type_definitions", table =>
        {
            table.HasCheckConstraint("ck_work_item_type_definitions_order", "\"order\" BETWEEN 0 AND 10000");
            table.HasCheckConstraint("ck_work_item_type_definitions_version", "version > 0");
        });
        builder.HasKey(definition => new { definition.TenantId, definition.Id });
        builder.Property(definition => definition.TenantId).HasColumnName("tenant_id");
        builder.Property(definition => definition.Id).HasColumnName("id").HasConversion<string>().HasMaxLength(32);
        builder.Property(definition => definition.Label).HasColumnName("label").HasMaxLength(80).IsRequired();
        builder.Property(definition => definition.Description).HasColumnName("description").HasMaxLength(500).IsRequired();
        builder.Property(definition => definition.Order).HasColumnName("order");
        builder.Property(definition => definition.ColorToken).HasColumnName("color_token").HasMaxLength(32).IsRequired();
        builder.Property(definition => definition.Enabled).HasColumnName("enabled");
        builder.Property(definition => definition.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(definition => definition.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(definition => new { definition.TenantId, definition.Order });
    }
}
