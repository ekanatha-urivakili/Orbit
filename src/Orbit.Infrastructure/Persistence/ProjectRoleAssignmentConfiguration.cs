using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Access;
using Orbit.Domain.Projects;

namespace Orbit.Infrastructure.Persistence;

internal sealed class ProjectRoleAssignmentConfiguration : IEntityTypeConfiguration<ProjectRoleAssignment>
{
    public void Configure(EntityTypeBuilder<ProjectRoleAssignment> builder)
    {
        builder.ToTable("project_role_assignments");
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(assignment => assignment.TenantId).HasColumnName("tenant_id");
        builder.Property(assignment => assignment.ProjectId).HasColumnName("project_id");
        builder.Property(assignment => assignment.MembershipId).HasColumnName("membership_id");
        builder.Property(assignment => assignment.RoleId).HasColumnName("role_id");
        builder.Property(assignment => assignment.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(assignment => new
        {
            assignment.TenantId,
            assignment.ProjectId,
            assignment.MembershipId
        }).IsUnique();
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(assignment => new { assignment.TenantId, assignment.ProjectId })
            .HasPrincipalKey(project => new { project.TenantId, project.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<TenantMembership>()
            .WithMany()
            .HasForeignKey(assignment => new { assignment.TenantId, assignment.MembershipId })
            .HasPrincipalKey(membership => new { membership.TenantId, membership.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(assignment => assignment.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
