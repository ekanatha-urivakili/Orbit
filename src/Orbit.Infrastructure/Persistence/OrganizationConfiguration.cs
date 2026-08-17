using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Organizations;

namespace Orbit.Infrastructure.Persistence;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations", table =>
            table.HasCheckConstraint("ck_organizations_slug", "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'"));
        builder.HasKey(organization => organization.Id);
        builder.Property(organization => organization.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(organization => organization.Slug).HasColumnName("slug").HasMaxLength(63).IsRequired();
        builder.Property(organization => organization.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(organization => organization.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(organization => organization.Slug).IsUnique();
    }
}

internal sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable("organization_memberships", table =>
            table.HasCheckConstraint(
                "ck_organization_memberships_role",
                "role IN ('Owner', 'Administrator', 'Member')"));
        builder.HasKey(membership => membership.Id);
        builder.Property(membership => membership.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(membership => membership.OrganizationId).HasColumnName("organization_id");
        builder.Property(membership => membership.UserId).HasColumnName("user_id");
        builder.Property(membership => membership.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(membership => membership.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(membership => new { membership.OrganizationId, membership.UserId }).IsUnique();
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(membership => membership.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
