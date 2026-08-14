using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Configuration;

namespace Orbit.Infrastructure.Persistence;

internal sealed class CustomFieldDefinitionConfiguration : IEntityTypeConfiguration<CustomFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CustomFieldDefinition> builder)
    {
        builder.ToTable("custom_field_definitions", table =>
        {
            table.HasCheckConstraint("ck_custom_field_definitions_order", "\"order\" BETWEEN 0 AND 10000");
            table.HasCheckConstraint("ck_custom_field_definitions_version", "version > 0");
        });
        builder.HasKey(definition => definition.Id);
        builder.Property(definition => definition.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(definition => definition.TenantId).HasColumnName("tenant_id");
        builder.Property(definition => definition.Key).HasColumnName("key").HasMaxLength(64).IsRequired();
        builder.Property(definition => definition.Label).HasColumnName("label").HasMaxLength(80).IsRequired();
        builder.Property(definition => definition.FieldType).HasColumnName("field_type").HasConversion<string>().HasMaxLength(32);
        builder.Property(definition => definition.Required).HasColumnName("required");
        builder.Property(definition => definition.Order).HasColumnName("order");
        builder.Property(definition => definition.Enabled).HasColumnName("enabled");
        builder.Property(definition => definition.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(definition => definition.CreatedAt).HasColumnName("created_at");
        builder.Property(definition => definition.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(definition => new { definition.TenantId, definition.Key }).IsUnique();
        builder.HasIndex(definition => new { definition.TenantId, definition.Order });
    }
}
