using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Access;
using Orbit.Domain.Directory;
using Orbit.Domain.Projects;

namespace Orbit.Infrastructure.Persistence;

internal sealed class ProjectGroupRoleAssignmentConfiguration : IEntityTypeConfiguration<ProjectGroupRoleAssignment>
{
    public void Configure(EntityTypeBuilder<ProjectGroupRoleAssignment> builder)
    {
        builder.ToTable("project_group_role_assignments", table =>
            table.HasCheckConstraint(
                "ck_project_group_role_assignments_role",
                "project_role IN ('Administrator', 'Member', 'Viewer')"));
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(assignment => assignment.TenantId).HasColumnName("tenant_id");
        builder.Property(assignment => assignment.ProjectId).HasColumnName("project_id");
        builder.Property(assignment => assignment.GroupId).HasColumnName("group_id");
        builder.Property(assignment => assignment.Role)
            .HasColumnName("project_role")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(assignment => assignment.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(assignment => new
        {
            assignment.TenantId,
            assignment.ProjectId,
            assignment.GroupId
        }).IsUnique();
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(assignment => new { assignment.TenantId, assignment.ProjectId })
            .HasPrincipalKey(project => new { project.TenantId, project.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DirectoryGroup>()
            .WithMany()
            .HasForeignKey(assignment => new { assignment.TenantId, assignment.GroupId })
            .HasPrincipalKey(group => new { group.TenantId, group.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
