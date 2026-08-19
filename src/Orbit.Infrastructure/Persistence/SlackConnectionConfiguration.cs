using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Integrations;
using Orbit.Domain.Projects;

namespace Orbit.Infrastructure.Persistence;

internal sealed class SlackConnectionConfiguration : IEntityTypeConfiguration<SlackConnection>
{
    public void Configure(EntityTypeBuilder<SlackConnection> builder)
    {
        builder.ToTable("slack_connections");

        builder.HasKey(connection => connection.Id);
        builder.Property(connection => connection.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(connection => connection.TenantId).HasColumnName("tenant_id");
        builder.Property(connection => connection.ProjectId).HasColumnName("project_id");
        builder.Property(connection => connection.TeamId).HasColumnName("team_id").HasMaxLength(64);
        builder.Property(connection => connection.TeamName).HasColumnName("team_name").HasMaxLength(255);
        builder.Property(connection => connection.ChannelId).HasColumnName("channel_id").HasMaxLength(64);
        builder.Property(connection => connection.ChannelName).HasColumnName("channel_name").HasMaxLength(255);
        builder.Property(connection => connection.EncryptedWebhookUrl).HasColumnName("encrypted_webhook_url");
        builder.Property(connection => connection.ConnectedByUserId).HasColumnName("connected_by_user_id");
        builder.Property(connection => connection.CreatedAt).HasColumnName("created_at");

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(connection => new { connection.TenantId, connection.ProjectId })
            .HasPrincipalKey(project => new { project.TenantId, project.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(connection => new { connection.TenantId, connection.ProjectId })
            .IsUnique()
            .HasDatabaseName("ux_slack_connections_tenant_project");
    }
}
