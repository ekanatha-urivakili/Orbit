using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Workspaces;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("workspaces", table =>
        {
            table.HasCheckConstraint("ck_workspaces_slug", "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
            table.HasCheckConstraint("ck_workspaces_authorization_epoch", "authorization_epoch > 0");
        });
        builder.HasKey(workspace => workspace.Id);
        builder.Property(workspace => workspace.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(workspace => workspace.OrganizationId).HasColumnName("organization_id");
        builder.Property(workspace => workspace.Slug).HasColumnName("slug").HasMaxLength(63).IsRequired();
        builder.Property(workspace => workspace.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(workspace => workspace.AuthorizationEpoch).HasColumnName("authorization_epoch");
        builder.Property(workspace => workspace.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(workspace => workspace.Slug).IsUnique();
        builder.HasIndex(workspace => workspace.OrganizationId);
        builder.HasOne<Orbit.Domain.Organizations.Organization>()
            .WithMany()
            .HasForeignKey(workspace => workspace.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
