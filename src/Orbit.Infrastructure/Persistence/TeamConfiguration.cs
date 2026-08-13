using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Access;
using Orbit.Domain.Directory;

namespace Orbit.Infrastructure.Persistence;

internal sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("teams");
        builder.HasKey(team => team.Id);
        builder.HasAlternateKey(team => new { team.TenantId, team.Id });
        builder.Property(team => team.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(team => team.TenantId).HasColumnName("tenant_id");
        builder.Property(team => team.Name).HasColumnName("name").HasMaxLength(120);
        builder.Property(team => team.CreatedByMembershipId).HasColumnName("created_by_membership_id");
        builder.Property(team => team.CreatedAt).HasColumnName("created_at");
        builder.Property(team => team.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(team => new { team.TenantId, team.Name }).IsUnique();
        builder.HasOne<TenantMembership>()
            .WithMany()
            .HasForeignKey(team => new { team.TenantId, team.CreatedByMembershipId })
            .HasPrincipalKey(membership => new { membership.TenantId, membership.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TeamMembershipConfiguration : IEntityTypeConfiguration<TeamMembership>
{
    public void Configure(EntityTypeBuilder<TeamMembership> builder)
    {
        builder.ToTable("team_memberships");
        builder.HasKey(membership => membership.Id);
        builder.Property(membership => membership.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(membership => membership.TenantId).HasColumnName("tenant_id");
        builder.Property(membership => membership.TeamId).HasColumnName("team_id");
        builder.Property(membership => membership.MembershipId).HasColumnName("membership_id");
        builder.Property(membership => membership.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(membership => new { membership.TenantId, membership.TeamId, membership.MembershipId })
            .IsUnique();
        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(membership => new { membership.TenantId, membership.TeamId })
            .HasPrincipalKey(team => new { team.TenantId, team.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<TenantMembership>()
            .WithMany()
            .HasForeignKey(membership => new { membership.TenantId, membership.MembershipId })
            .HasPrincipalKey(tenantMembership => new { tenantMembership.TenantId, tenantMembership.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
