using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Access;
using Orbit.Domain.Identity;

namespace Orbit.Infrastructure.Persistence;

internal sealed class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable("tenant_memberships", table =>
        {
            table.HasCheckConstraint(
                "ck_tenant_memberships_principal_type",
                "principal_type IN ('User', 'ServiceAccount')");
            table.HasCheckConstraint(
                "ck_tenant_memberships_role",
                "tenant_role IN ('Owner', 'Administrator', 'Member')");
            table.HasCheckConstraint(
                "ck_tenant_memberships_identity",
                "(user_id IS NOT NULL AND issuer IS NULL AND subject IS NULL AND principal_type = 'User') OR " +
                "(user_id IS NULL AND issuer IS NOT NULL AND subject IS NOT NULL)");
        });
        builder.HasKey(membership => membership.Id);
        builder.HasAlternateKey(membership => new { membership.TenantId, membership.Id });
        builder.Property(membership => membership.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(membership => membership.TenantId).HasColumnName("tenant_id");
        builder.Property(membership => membership.UserId).HasColumnName("user_id");
        builder.Property(membership => membership.Issuer).HasColumnName("issuer").HasMaxLength(512);
        builder.Property(membership => membership.Subject).HasColumnName("subject").HasMaxLength(255);
        builder.Property(membership => membership.PrincipalType)
            .HasColumnName("principal_type")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(membership => membership.Role)
            .HasColumnName("tenant_role")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(membership => membership.IsActive).HasColumnName("is_active");
        builder.Property(membership => membership.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(membership => new { membership.TenantId, membership.Issuer, membership.Subject })
            .IsUnique();
        builder.HasIndex(membership => new { membership.TenantId, membership.UserId })
            .IsUnique()
            .HasFilter("user_id IS NOT NULL");
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
