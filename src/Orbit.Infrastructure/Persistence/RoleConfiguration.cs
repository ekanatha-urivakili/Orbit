using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Access;

namespace Orbit.Infrastructure.Persistence;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(role => role.TenantId).HasColumnName("tenant_id");
        builder.Property(role => role.Name).HasColumnName("name").HasMaxLength(100);
        builder.Property(role => role.IsSystem).HasColumnName("is_system");
        builder.Property(role => role.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(role => new { role.TenantId, role.Name }).IsUnique();

        builder.HasMany(role => role.Permissions)
            .WithOne()
            .HasForeignKey(permission => permission.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(role => role.Permissions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");
        builder.HasKey(permission => new { permission.RoleId, permission.Permission });
        builder.Property(permission => permission.RoleId).HasColumnName("role_id");
        builder.Property(permission => permission.Permission)
            .HasColumnName("permission")
            .HasConversion<string>()
            .HasMaxLength(32);
    }
}
