using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Identity;

namespace Orbit.Infrastructure.Persistence;

internal sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("user_accounts", table =>
            table.HasCheckConstraint("ck_user_accounts_status", "status IN ('Active', 'Disabled')"));
        builder.HasKey(account => account.Id);
        builder.Property(account => account.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(account => account.NormalizedEmail)
            .HasColumnName("normalized_email")
            .HasMaxLength(320)
            .IsRequired();
        builder.Property(account => account.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(120)
            .IsRequired();
        builder.Property(account => account.AvatarUrl)
            .HasColumnName("avatar_url")
            .HasMaxLength(2048);
        builder.Property(account => account.Version)
            .HasColumnName("version")
            .IsConcurrencyToken();
        builder.Property(account => account.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(account => account.EmailVerifiedAt).HasColumnName("email_verified_at");
        builder.Property(account => account.CreatedAt).HasColumnName("created_at");
        builder.Property(account => account.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(account => account.NormalizedEmail).IsUnique();
    }
}

internal sealed class ExternalIdentityConfiguration : IEntityTypeConfiguration<ExternalIdentity>
{
    public void Configure(EntityTypeBuilder<ExternalIdentity> builder)
    {
        builder.ToTable("external_identities");
        builder.HasKey(identity => identity.Id);
        builder.Property(identity => identity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(identity => identity.UserId).HasColumnName("user_id");
        builder.Property(identity => identity.Issuer).HasColumnName("issuer").HasMaxLength(512).IsRequired();
        builder.Property(identity => identity.Subject).HasColumnName("subject").HasMaxLength(255).IsRequired();
        builder.Property(identity => identity.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(identity => new { identity.Issuer, identity.Subject }).IsUnique();
        builder.HasIndex(identity => identity.UserId);
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(identity => identity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LocalCredentialConfiguration : IEntityTypeConfiguration<LocalCredential>
{
    public void Configure(EntityTypeBuilder<LocalCredential> builder)
    {
        builder.ToTable("local_credentials");
        builder.HasKey(credential => credential.UserId);
        builder.Property(credential => credential.UserId).HasColumnName("user_id").ValueGeneratedNever();
        builder.Property(credential => credential.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(512)
            .IsRequired();
        builder.Property(credential => credential.HashAlgorithm)
            .HasColumnName("hash_algorithm")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(credential => credential.HashParametersVersion)
            .HasColumnName("hash_parameters_version");
        builder.Property(credential => credential.ChangedAt).HasColumnName("changed_at");
        builder.HasOne<UserAccount>()
            .WithOne()
            .HasForeignKey<LocalCredential>(credential => credential.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
{
    public void Configure(EntityTypeBuilder<RefreshSession> builder)
    {
        builder.ToTable("refresh_sessions", table =>
        {
            table.HasCheckConstraint(
                "ck_refresh_sessions_status",
                "status IN ('Active', 'Rotated', 'Revoked')");
            table.HasCheckConstraint("ck_refresh_sessions_version", "version > 0");
        });
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(session => session.UserId).HasColumnName("user_id");
        builder.Property(session => session.TenantId).HasColumnName("tenant_id");
        builder.Property(session => session.FamilyId).HasColumnName("family_id");
        builder.Property(session => session.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(session => session.UserAgent).HasColumnName("user_agent").HasMaxLength(512);
        builder.Property(session => session.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
        builder.Property(session => session.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        builder.Property(session => session.CreatedAt).HasColumnName("created_at");
        builder.Property(session => session.LastUsedAt).HasColumnName("last_used_at");
        builder.Property(session => session.ExpiresAt).HasColumnName("expires_at");
        builder.Property(session => session.RevokedAt).HasColumnName("revoked_at");
        builder.Property(session => session.ReplacedBySessionId).HasColumnName("replaced_by_session_id");
        builder.Property(session => session.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(session => session.TokenHash).IsUnique();
        builder.HasIndex(session => new { session.UserId, session.Status });
        builder.HasIndex(session => session.FamilyId);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SiteRoleAssignmentConfiguration : IEntityTypeConfiguration<SiteRoleAssignment>
{
    public void Configure(EntityTypeBuilder<SiteRoleAssignment> builder)
    {
        builder.ToTable("site_role_assignments", table =>
            table.HasCheckConstraint("ck_site_role_assignments_role", "site_role = 'SuperAdministrator'"));
        builder.HasKey(assignment => new { assignment.UserId, assignment.Role });
        builder.Property(assignment => assignment.UserId).HasColumnName("user_id").ValueGeneratedNever();
        builder.Property(assignment => assignment.Role)
            .HasColumnName("site_role")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(assignment => assignment.GrantedAt).HasColumnName("granted_at");
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(assignment => assignment.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
