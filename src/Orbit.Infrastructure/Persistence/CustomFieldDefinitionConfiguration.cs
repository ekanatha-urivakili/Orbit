using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Orbit.Domain.Choices;
using Orbit.Domain.Configuration;
using Orbit.Domain.Projects;

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
        builder.HasKey(definition => new { definition.TenantId, definition.Id });
        builder.Property(definition => definition.TenantId).HasColumnName("tenant_id");
        builder.Property(definition => definition.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(definition => definition.ProjectId).HasColumnName("project_id");
        builder.Property(definition => definition.Key).HasColumnName("key").HasMaxLength(64).IsRequired();
        builder.Property(definition => definition.Label).HasColumnName("label").HasMaxLength(80).IsRequired();
        builder.Property(definition => definition.FieldType).HasColumnName("field_type").HasConversion<string>().HasMaxLength(32);
        builder.Property(definition => definition.Required).HasColumnName("required");
        builder.Property(definition => definition.Order).HasColumnName("order");
        builder.Property(definition => definition.Enabled).HasColumnName("enabled");

        var applicableTypesConverter = new ValueConverter<IReadOnlyList<WorkItemType>, string[]>(
            types => types.Select(type => type.ToString()).ToArray(),
            values => values.Select(Enum.Parse<WorkItemType>).ToArray());
        var applicableTypesComparer = new ValueComparer<IReadOnlyList<WorkItemType>>(
            (left, right) => left!.SequenceEqual(right!),
            types => types.Aggregate(0, (hash, type) => HashCode.Combine(hash, type)),
            types => types.ToArray());
        builder.Property(definition => definition.ApplicableTypes)
            .HasColumnName("applicable_types")
            .HasColumnType("text[]")
            .HasConversion(applicableTypesConverter, applicableTypesComparer);
        builder.Property(definition => definition.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(definition => definition.CreatedAt).HasColumnName("created_at");
        builder.Property(definition => definition.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(definition => new { definition.TenantId, definition.ProjectId, definition.Key }).IsUnique();
        builder.HasIndex(definition => new { definition.TenantId, definition.ProjectId, definition.Order });
        builder.HasOne<Project>().WithMany().HasForeignKey(definition => new { definition.TenantId, definition.ProjectId })
            .HasPrincipalKey(project => new { project.TenantId, project.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(definition => definition.ChoiceOptions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.OwnsMany(definition => definition.ChoiceOptions, option =>
        {
            option.ToTable("custom_field_choice_options");
            option.WithOwner().HasForeignKey("tenant_id", "custom_field_definition_id");
            option.Property<Guid>("tenant_id").HasColumnName("tenant_id");
            option.Property<Guid>("custom_field_definition_id").HasColumnName("custom_field_definition_id");
            option.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();
            option.Property(o => o.Label).HasColumnName("label").HasMaxLength(80).IsRequired();
            option.Property(o => o.Order).HasColumnName("order");
            option.HasKey("tenant_id", "custom_field_definition_id", nameof(CustomFieldChoiceOption.Id));
        });
    }
}
