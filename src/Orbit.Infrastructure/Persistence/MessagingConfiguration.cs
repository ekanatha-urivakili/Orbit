using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Messaging;

namespace Orbit.Infrastructure.Persistence;

internal sealed class OutboxEmailMessageConfiguration : IEntityTypeConfiguration<OutboxEmailMessage>
{
    public void Configure(EntityTypeBuilder<OutboxEmailMessage> builder)
    {
        builder.ToTable("outbox_email_messages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(message => message.ToEmail).HasColumnName("to_email").HasMaxLength(320).IsRequired();
        builder.Property(message => message.Subject).HasColumnName("subject").HasMaxLength(512).IsRequired();
        builder.Property(message => message.HtmlBody).HasColumnName("html_body").IsRequired();
        builder.Property(message => message.TenantId).HasColumnName("tenant_id");
        builder.Property(message => message.WorkspaceInvitationId).HasColumnName("workspace_invitation_id");
        builder.Property(message => message.FrontendBaseUrl).HasColumnName("frontend_base_url").HasMaxLength(2048);
        builder.Property(message => message.CreatedAt).HasColumnName("created_at");
        builder.Property(message => message.PublishedAt).HasColumnName("published_at");
        builder.Property(message => message.Attempts).HasColumnName("attempts");
        builder.Property(message => message.LastError).HasColumnName("last_error").HasMaxLength(2048);
        builder.HasIndex(message => new { message.PublishedAt, message.Attempts, message.CreatedAt });
    }
}
