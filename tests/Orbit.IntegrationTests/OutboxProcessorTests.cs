using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Domain.Messaging;
using Orbit.Infrastructure.Email;
using Orbit.Infrastructure.Messaging;
using Orbit.Infrastructure.Persistence;

namespace Orbit.IntegrationTests;

public sealed class OutboxProcessorTests : IClassFixture<OrbitApiFactory>
{
    private readonly OrbitApiFactory _factory;

    public OutboxProcessorTests(OrbitApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProcessPendingAsync_SendsEmailsAndMarksPublished()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrbitDbContext>();
        var emailSender = new FakeEmailSender();
        var processor = new OutboxEmailProcessor(
            dbContext,
            emailSender,
            TimeProvider.System,
            NullLogger<OutboxEmailProcessor>.Instance);

        var message = OutboxEmailMessage.Create(
            "recipient@example.test",
            "Test Subject",
            "<p>Test Body</p>",
            DateTimeOffset.UtcNow);

        await dbContext.OutboxEmailMessages.AddAsync(message);
        await dbContext.SaveChangesAsync();

        await processor.ProcessPendingAsync(CancellationToken.None);

        var updated = await dbContext.OutboxEmailMessages
            .AsNoTracking()
            .SingleAsync(m => m.Id == message.Id);

        Assert.NotNull(updated.PublishedAt);
        Assert.Contains(emailSender.SentMessages, m => m.ToEmail == "recipient@example.test");
    }

    [Fact]
    public async Task ProcessPendingAsync_OnSendFailure_RecordsAttemptAndLastError()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrbitDbContext>();
        var emailSender = new FailingEmailSender();
        var processor = new OutboxEmailProcessor(
            dbContext,
            emailSender,
            TimeProvider.System,
            NullLogger<OutboxEmailProcessor>.Instance);

        var message = OutboxEmailMessage.Create(
            "fail@example.test",
            "Failing Subject",
            "<p>Test Body</p>",
            DateTimeOffset.UtcNow);

        await dbContext.OutboxEmailMessages.AddAsync(message);
        await dbContext.SaveChangesAsync();

        await processor.ProcessPendingAsync(CancellationToken.None);

        var updated = await dbContext.OutboxEmailMessages
            .AsNoTracking()
            .SingleAsync(m => m.Id == message.Id);

        Assert.Null(updated.PublishedAt);
        Assert.Equal(1, updated.Attempts);
        Assert.Contains("SMTP connection failed", updated.LastError);
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<(string ToEmail, string Subject, string HtmlBody)> SentMessages { get; } = [];

        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
        {
            SentMessages.Add((toEmail, subject, htmlBody));
            return Task.CompletedTask;
        }
    }

    private sealed class FailingEmailSender : IEmailSender
    {
        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("SMTP connection failed.");
    }
}
