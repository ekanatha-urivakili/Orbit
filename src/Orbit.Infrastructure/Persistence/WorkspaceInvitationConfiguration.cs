using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Access;
using Orbit.Domain.Directory;
using Orbit.Domain.Identity;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkspaceInvitationConfiguration : IEntityTypeConfiguration<WorkspaceInvitation>
{
    public void Configure(EntityTypeBuilder<WorkspaceInvitation> builder)
    {
        builder.ToTable("workspace_invitations", table =>
        {
            table.HasCheckConstraint(
                "ck_workspace_invitations_role",
                "tenant_role IN ('Administrator', 'Member')");
            table.HasCheckConstraint(
                "ck_workspace_invitations_status",
                "status IN ('Active', 'Accepted', 'Revoked')");
            table.HasCheckConstraint("ck_workspace_invitations_version", "version > 0");
        });
        builder.HasKey(invitation => invitation.Id);
        builder.HasAlternateKey(invitation => new { invitation.TenantId, invitation.Id });
        builder.Property(invitation => invitation.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(invitation => invitation.TenantId).HasColumnName("tenant_id");
        builder.Property(invitation => invitation.NormalizedEmail)
            .HasColumnName("normalized_email").HasMaxLength(320).IsRequired();
        builder.Property(invitation => invitation.Role)
            .HasColumnName("tenant_role").HasConversion<string>().HasMaxLength(32);
        builder.Property(invitation => invitation.TeamId).HasColumnName("team_id");
        builder.Property(invitation => invitation.TokenHash)
            .HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(invitation => invitation.InvitedByMembershipId).HasColumnName("invited_by_membership_id");
        builder.Property(invitation => invitation.Status)
            .HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        builder.Property(invitation => invitation.CreatedAt).HasColumnName("created_at");
        builder.Property(invitation => invitation.UpdatedAt).HasColumnName("updated_at");
        builder.Property(invitation => invitation.ExpiresAt).HasColumnName("expires_at");
        builder.Property(invitation => invitation.AcceptedAt).HasColumnName("accepted_at");
        builder.Property(invitation => invitation.AcceptedByUserId).HasColumnName("accepted_by_user_id");
        builder.Property(invitation => invitation.Version)
            .HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();
        builder.HasIndex(invitation => new { invitation.TenantId, invitation.NormalizedEmail })
            .IsUnique()
            .HasFilter("status = 'Active'");
        builder.HasOne<TenantMembership>()
            .WithMany()
            .HasForeignKey(invitation => new { invitation.TenantId, invitation.InvitedByMembershipId })
            .HasPrincipalKey(membership => new { membership.TenantId, membership.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(invitation => new { invitation.TenantId, invitation.TeamId })
            .HasPrincipalKey(team => new { team.TenantId, team.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(invitation => invitation.AcceptedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
