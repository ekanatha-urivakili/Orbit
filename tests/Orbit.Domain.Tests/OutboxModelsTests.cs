using Orbit.Domain.Common;
using Orbit.Domain.Messaging;

namespace Orbit.Domain.Tests;

public sealed class OutboxModelsTests
{
    [Fact]
    public void OutboxEmailMessage_MarkPublishedSetsPublishedAt()
    {
        var now = DateTimeOffset.UtcNow;
        var message = OutboxEmailMessage.Create("user@example.test", "Subject", "<p>Body</p>", now);

        message.MarkPublished(now.AddSeconds(5));

        Assert.Equal(now.AddSeconds(5), message.PublishedAt);
    }

    [Fact]
    public void OutboxEmailMessage_RecordFailureIncrementsAttemptsAndStoresError()
    {
        var message = OutboxEmailMessage.Create("user@example.test", "Subject", "<p>Body</p>", DateTimeOffset.UtcNow);

        message.RecordFailure("smtp timeout");
        message.RecordFailure("smtp timeout");

        Assert.Equal(2, message.Attempts);
        Assert.Equal("smtp timeout", message.LastError);
        Assert.Null(message.PublishedAt);
    }

    [Fact]
    public void OutboxEmailMessage_Create_RejectsMissingRecipient()
    {
        var action = () => OutboxEmailMessage.Create(" ", "Subject", "<p>Body</p>", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }
}
