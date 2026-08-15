using Orbit.Domain.Common;

namespace Orbit.Domain.Messaging;

/// <summary>
/// A transactional-outbox row for one email to send. Written in the same <c>SaveChangesAsync</c>
/// call as the domain change that triggered it (e.g. a password-reset token), so the two commit
/// atomically; <see cref="Orbit.Worker"/> polls and delivers it out of band. Global, not
/// tenant-scoped - see ADR-014.
/// </summary>
public sealed class OutboxEmailMessage
{
    private OutboxEmailMessage()
    {
    }

    private OutboxEmailMessage(
        Guid id,
        string toEmail,
        string subject,
        string htmlBody,
        Guid? tenantId,
        Guid? workspaceInvitationId,
        string? frontendBaseUrl,
        DateTimeOffset now)
    {
        Id = id;
        ToEmail = toEmail;
        Subject = subject;
        HtmlBody = htmlBody;
        TenantId = tenantId;
        WorkspaceInvitationId = workspaceInvitationId;
        FrontendBaseUrl = frontendBaseUrl;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public string ToEmail { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string HtmlBody { get; private set; } = string.Empty;
    public Guid? TenantId { get; private set; }
    public Guid? WorkspaceInvitationId { get; private set; }
    public string? FrontendBaseUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }

    public static OutboxEmailMessage Create(string toEmail, string subject, string htmlBody, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(toEmail) || toEmail.Length > 320)
        {
            throw new DomainException("A valid recipient email address is required.");
        }

        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 512)
        {
            throw new DomainException("An email subject of 1 to 512 characters is required.");
        }

        if (string.IsNullOrWhiteSpace(htmlBody))
        {
            throw new DomainException("An email body is required.");
        }

        return new OutboxEmailMessage(
            Guid.CreateVersion7(), toEmail.Trim(), subject.Trim(), htmlBody, null, null, null, now);
    }

    public static OutboxEmailMessage CreateWorkspaceInvitation(
        string toEmail,
        string subject,
        Guid tenantId,
        Guid invitationId,
        string frontendBaseUrl,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || invitationId == Guid.Empty)
        {
            throw new DomainException("Tenant and invitation ids are required.");
        }

        if (!Uri.TryCreate(frontendBaseUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw new DomainException("Frontend base URL must be absolute HTTP or HTTPS.");
        }

        var message = Create(toEmail, subject, "<p>Invitation delivery is generated at send time.</p>", now);
        message.TenantId = tenantId;
        message.WorkspaceInvitationId = invitationId;
        message.FrontendBaseUrl = frontendBaseUrl;
        return message;
    }

    public void MarkPublished(DateTimeOffset now)
    {
        PublishedAt = now;
    }

    public void RecordFailure(string error)
    {
        Attempts++;
        LastError = error.Length > 2048 ? error[..2048] : error;
    }
}
