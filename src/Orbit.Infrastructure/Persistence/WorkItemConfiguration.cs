using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Directory;
using Orbit.Domain.Projects;
using Orbit.Domain.Configuration;
using Orbit.Domain.WorkItems;

namespace Orbit.Infrastructure.Persistence;

internal sealed class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.ToTable("work_items", table =>
        {
            table.HasCheckConstraint("ck_work_items_sequence", "sequence_number > 0");
            table.HasCheckConstraint("ck_work_items_version", "version > 0");
            table.HasCheckConstraint("ck_work_items_story_points", "story_points IS NULL OR story_points BETWEEN 0 AND 10000");
            table.HasCheckConstraint("ck_work_items_epic_name", "type <> 'Epic' OR epic_name IS NOT NULL");
            table.HasCheckConstraint(
                "ck_work_items_type",
                "type IN ('Initiative', 'Epic', 'Task', 'Story', 'Spike', 'Test', 'Feature', 'Request', 'Bug', 'Subtask')");
            table.HasCheckConstraint(
                "ck_work_items_status",
                "status IN ('Backlog', 'Selected', 'InProgress', 'InReview', 'Done', 'Blocked')");
            table.HasCheckConstraint(
                "ck_work_items_priority",
                "priority IN ('Lowest', 'Low', 'Medium', 'High', 'Highest')");
        });
        builder.HasKey(workItem => workItem.Id);
        builder.Property(workItem => workItem.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(workItem => workItem.TenantId).HasColumnName("tenant_id");
        builder.Property(workItem => workItem.ProjectId).HasColumnName("project_id");
        builder.Property(workItem => workItem.SequenceNumber).HasColumnName("sequence_number");
        builder.Property(workItem => workItem.Key).HasColumnName("key").HasMaxLength(32).IsRequired();
        builder.Property(workItem => workItem.Summary).HasColumnName("summary").HasMaxLength(255).IsRequired();
        builder.Property(workItem => workItem.Description).HasColumnName("description").HasMaxLength(32_000);
        builder.Property(workItem => workItem.ParentId).HasColumnName("parent_id");
        builder.Property(workItem => workItem.EpicName).HasColumnName("epic_name").HasMaxLength(255);
        builder.Property(workItem => workItem.AcceptanceCriteria).HasColumnName("acceptance_criteria").HasMaxLength(32_000);
        builder.Property(workItem => workItem.StepsToConduct).HasColumnName("steps_to_conduct").HasMaxLength(32_000);
        builder.Property(workItem => workItem.AssigneeUserId).HasColumnName("assignee_user_id");
        builder.Property(workItem => workItem.DeveloperUserId).HasColumnName("developer_user_id");
        builder.Property(workItem => workItem.ProductOwnerUserId).HasColumnName("product_owner_user_id");
        builder.Property(workItem => workItem.SprintName).HasColumnName("sprint_name").HasMaxLength(255);
        builder.Property(workItem => workItem.IdentifiedOn).HasColumnName("identified_on").HasMaxLength(255);
        builder.Property(workItem => workItem.StartDate).HasColumnName("start_date").HasColumnType("date");
        builder.Property(workItem => workItem.DueDate).HasColumnName("due_date").HasColumnType("date");
        builder.Property(workItem => workItem.TeamId).HasColumnName("team_id");
        builder.Property(workItem => workItem.StoryPoints).HasColumnName("story_points").HasPrecision(10, 2);
        builder.Property(workItem => workItem.Labels).HasColumnName("labels").HasColumnType("text[]");
        builder.Property(workItem => workItem.Countries).HasColumnName("countries").HasColumnType("text[]");
        builder.Property(workItem => workItem.AttachmentNames).HasColumnName("attachment_names").HasColumnType("text[]");
        builder.Property(workItem => workItem.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(32);
        builder.Property(workItem => workItem.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32);
        builder.Property(workItem => workItem.Priority).HasColumnName("priority").HasConversion<string>().HasMaxLength(32);
        builder.Property(workItem => workItem.Rank).HasColumnName("rank").HasPrecision(38, 16);
        builder.Property(workItem => workItem.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(workItem => workItem.CreatedAt).HasColumnName("created_at");
        builder.Property(workItem => workItem.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(workItem => new { workItem.TenantId, workItem.Key }).IsUnique();
        builder.HasIndex(workItem => new { workItem.TenantId, workItem.ProjectId, workItem.Rank });
        builder.HasIndex(workItem => new { workItem.TenantId, workItem.ProjectId, workItem.Status, workItem.Rank });
        builder.HasIndex(workItem => new { workItem.TenantId, workItem.ParentId });
        builder.HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(workItem => new { workItem.TenantId, workItem.ParentId })
            .HasPrincipalKey(workItem => new { workItem.TenantId, workItem.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(workItem => new { workItem.TenantId, workItem.ProjectId })
            .HasPrincipalKey(project => new { project.TenantId, project.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkItemTypeDefinition>()
            .WithMany()
            .HasForeignKey(workItem => new { workItem.TenantId, Id = workItem.Type })
            .HasPrincipalKey(definition => new { definition.TenantId, definition.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(workItem => new { workItem.TenantId, workItem.TeamId })
            .HasPrincipalKey(team => new { team.TenantId, team.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
