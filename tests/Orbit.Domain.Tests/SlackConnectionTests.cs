using Orbit.Domain.Common;
using Orbit.Domain.Integrations;

namespace Orbit.Domain.Tests;

public sealed class SlackConnectionTests
{
    [Fact]
    public void Create_AssignsFields()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var connection = SlackConnection.Create(
            tenantId, projectId, "T1", "Acme", "C1", "general", "encrypted-webhook", userId, DateTimeOffset.UtcNow);

        Assert.Equal(tenantId, connection.TenantId);
        Assert.Equal(projectId, connection.ProjectId);
        Assert.Equal("Acme", connection.TeamName);
        Assert.Equal("general", connection.ChannelName);
        Assert.Equal("encrypted-webhook", connection.EncryptedWebhookUrl);
    }

    [Fact]
    public void Create_RejectsEmptyIds()
    {
        var action = () => SlackConnection.Create(
            Guid.Empty, Guid.NewGuid(), "T1", "Acme", "C1", "general", "encrypted-webhook", Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Create_RejectsMissingWebhook()
    {
        var action = () => SlackConnection.Create(
            Guid.NewGuid(), Guid.NewGuid(), "T1", "Acme", "C1", "general", "", Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }
}
