using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Access;
using Orbit.Domain.Directory;

namespace Orbit.Infrastructure.Persistence;

internal sealed class DirectoryGroupConfiguration : IEntityTypeConfiguration<DirectoryGroup>
{
    public void Configure(EntityTypeBuilder<DirectoryGroup> builder)
    {
        builder.ToTable("directory_groups");
        builder.HasKey(group => group.Id);
        builder.HasAlternateKey(group => new { group.TenantId, group.Id });
        builder.Property(group => group.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(group => group.TenantId).HasColumnName("tenant_id");
        builder.Property(group => group.Name).HasColumnName("name").HasMaxLength(120);
        builder.Property(group => group.CreatedByMembershipId).HasColumnName("created_by_membership_id");
        builder.Property(group => group.CreatedAt).HasColumnName("created_at");
        builder.Property(group => group.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(group => new { group.TenantId, group.Name }).IsUnique();
        builder.HasOne<TenantMembership>()
            .WithMany()
            .HasForeignKey(group => new { group.TenantId, group.CreatedByMembershipId })
            .HasPrincipalKey(membership => new { membership.TenantId, membership.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class GroupMembershipConfiguration : IEntityTypeConfiguration<GroupMembership>
{
    public void Configure(EntityTypeBuilder<GroupMembership> builder)
    {
        builder.ToTable("group_memberships");
        builder.HasKey(membership => membership.Id);
        builder.Property(membership => membership.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(membership => membership.TenantId).HasColumnName("tenant_id");
        builder.Property(membership => membership.GroupId).HasColumnName("group_id");
        builder.Property(membership => membership.MembershipId).HasColumnName("membership_id");
        builder.Property(membership => membership.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(membership => new { membership.TenantId, membership.GroupId, membership.MembershipId })
            .IsUnique();
        builder.HasOne<DirectoryGroup>()
            .WithMany()
            .HasForeignKey(membership => new { membership.TenantId, membership.GroupId })
            .HasPrincipalKey(group => new { group.TenantId, group.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<TenantMembership>()
            .WithMany()
            .HasForeignKey(membership => new { membership.TenantId, membership.MembershipId })
            .HasPrincipalKey(tenantMembership => new { tenantMembership.TenantId, tenantMembership.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
