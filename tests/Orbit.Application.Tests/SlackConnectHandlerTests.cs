using FluentValidation;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.Integrations;
using Orbit.Domain.Access;
using Orbit.Domain.Identity;
using Orbit.Domain.Integrations;
using Orbit.Domain.Projects;
using Orbit.Domain.WorkItems;
using Orbit.Domain.Workspaces;
using Orbit.Domain.Choices;

namespace Orbit.Application.Tests;

public sealed class SlackConnectHandlerTests
{
    [Fact]
    public async Task StartSlackConnect_BuildsAuthorizeUrlWithEncodedState()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var slackClient = new SlackClientStub();
        var handler = new StartSlackConnectHandler(
            new TenantContextStub(tenantId), new ProjectRepositoryStub(project), slackClient,
            new FakeOAuthStateCodec(), TimeProvider.System);

        var url = await handler.Handle(new StartSlackConnectCommand(project.Id), CancellationToken.None);

        Assert.Equal($"https://slack.example/authorize?state=slack-connect:{project.Id}", url);
    }

    [Fact]
    public async Task CompleteSlackOAuth_PersistsEncryptedConnectionAndReplacesExisting()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var existing = SlackConnection.Create(
            tenantId, project.Id, "T-OLD", "Old Team", "C-OLD", "old-channel", "old-encrypted", userId,
            DateTimeOffset.UtcNow);
        var connections = new SlackConnectionRepositoryStub(existing);
        var handler = new CompleteSlackOAuthHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(userId),
            new ProjectRepositoryStub(project),
            connections,
            new SlackClientStub(),
            new FakeOAuthStateCodec(),
            new FakeSecretProtector(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new CompleteSlackOAuthCommand("code-123", $"slack-connect:{project.Id}"), CancellationToken.None);

        Assert.Equal("Test Team", result.TeamName);
        Assert.Equal("general", result.ChannelName);
        Assert.Contains(existing, connections.Removed);
        var added = Assert.Single(connections.Added);
        Assert.Equal("protected(https://hooks.slack.example/webhook)", added.EncryptedWebhookUrl);
    }

    [Fact]
    public async Task CompleteSlackOAuth_InvalidState_ThrowsValidationException()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var handler = new CompleteSlackOAuthHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(Guid.NewGuid()),
            new ProjectRepositoryStub(project),
            new SlackConnectionRepositoryStub(),
            new SlackClientStub(),
            new FakeOAuthStateCodec(),
            new FakeSecretProtector(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new CompleteSlackOAuthCommand("code-123", "not-a-valid-state"), CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(action);
    }

    [Fact]
    public async Task PostWorkItemToSlack_NoConnection_ThrowsValidationException()
    {
        var tenantId = Guid.NewGuid();
        var workItem = WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Share this in Slack", null, WorkItemType.Task, Priority.Medium,
            DateTimeOffset.UtcNow);
        var handler = new PostWorkItemToSlackHandler(
            new TenantContextStub(tenantId), new WorkItemRepositoryStub(workItem),
            new SlackConnectionRepositoryStub(), new SlackClientStub(), new FakeSecretProtector());

        var action = () => handler.Handle(
            new PostWorkItemToSlackCommand(workItem.Id, null), CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(action);
    }

    [Fact]
    public async Task PostWorkItemToSlack_WithConnection_PostsDecryptedWebhook()
    {
        var tenantId = Guid.NewGuid();
        var workItem = WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Share this in Slack", null, WorkItemType.Task, Priority.Medium,
            DateTimeOffset.UtcNow);
        var connection = SlackConnection.Create(
            tenantId, workItem.ProjectId, "T1", "Team", "C1", "general",
            "protected(https://hooks.slack.example/webhook)", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var slackClient = new SlackClientStub();
        var handler = new PostWorkItemToSlackHandler(
            new TenantContextStub(tenantId), new WorkItemRepositoryStub(workItem),
            new SlackConnectionRepositoryStub(connection), slackClient, new FakeSecretProtector());

        await handler.Handle(new PostWorkItemToSlackCommand(workItem.Id, "Take a look"), CancellationToken.None);

        var posted = Assert.Single(slackClient.PostedMessages);
        Assert.Equal("https://hooks.slack.example/webhook", posted.WebhookUrl);
        Assert.Contains(workItem.Key, posted.Text);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class CurrentPrincipalStub(Guid? userId) : ICurrentPrincipal
    {
        public Guid? UserId => userId;
        public Guid? SessionId => null;
        public Guid MembershipId => Guid.NewGuid();
        public PrincipalType PrincipalType => PrincipalType.User;
        public TenantRole TenantRole => TenantRole.Member;
        public MembershipTier MembershipTier => MembershipTier.Standard;
        public bool IsDevelopmentBypass => true;
    }

    private sealed class ProjectRepositoryStub(Project project) : IProjectRepository
    {
        public Task AddAsync(Project value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Project?> GetAsync(
            Guid tenantId, Guid projectId, ProjectPermission permission, CancellationToken cancellationToken) =>
            Task.FromResult<Project?>(project.Id == projectId && project.TenantId == tenantId ? project : null);
        public Task<PagedResult<Project>> ListAsync(
            Guid tenantId, ProjectPermission permission, int skip, int take, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<Project>([project], 1));
    }

    private sealed class WorkItemRepositoryStub(WorkItem workItem) : IWorkItemRepository
    {
        public Task AddAsync(WorkItem value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<WorkItem?> GetAsync(
            Guid tenantId, Guid workItemId, ProjectPermission permission, CancellationToken cancellationToken) =>
            Task.FromResult<WorkItem?>(workItem.Id == workItemId && workItem.TenantId == tenantId ? workItem : null);
        public Task<PagedResult<WorkItem>> ListByProjectAsync(
            Guid tenantId, Guid projectId, ProjectPermission permission, int skip, int take,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<WorkItem>([], 0));
        public Task<IReadOnlyList<WorkItem>> ListByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> workItemIds, ProjectPermission permission,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItem>>([]);
        public Task<bool> HasChildrenAsync(Guid tenantId, Guid parentWorkItemId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task RemoveAsync(WorkItem workItem, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SlackConnectionRepositoryStub(params SlackConnection[] initial) : ISlackConnectionRepository
    {
        private readonly List<SlackConnection> connections = [.. initial];
        public List<SlackConnection> Added { get; } = [];
        public List<SlackConnection> Removed { get; } = [];

        public Task AddAsync(SlackConnection connection, CancellationToken cancellationToken)
        {
            connections.Add(connection);
            Added.Add(connection);
            return Task.CompletedTask;
        }

        public Task<SlackConnection?> GetByProjectAsync(
            Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(connections.SingleOrDefault(c => c.TenantId == tenantId && c.ProjectId == projectId));

        public Task<SlackConnection?> GetAsync(Guid tenantId, Guid connectionId, CancellationToken cancellationToken) =>
            Task.FromResult(connections.SingleOrDefault(c => c.TenantId == tenantId && c.Id == connectionId));

        public Task RemoveAsync(SlackConnection connection, CancellationToken cancellationToken)
        {
            connections.Remove(connection);
            Removed.Add(connection);
            return Task.CompletedTask;
        }
    }

    private sealed class SlackClientStub : ISlackClient
    {
        public List<(string WebhookUrl, string Text)> PostedMessages { get; } = [];

        public string BuildAuthorizeUrl(string state) => $"https://slack.example/authorize?state={state}";

        public Task<SlackIncomingWebhook> ExchangeCodeAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(new SlackIncomingWebhook(
                "xoxb-token", "T-TEST", "Test Team", "C-TEST", "general", "https://hooks.slack.example/webhook"));

        public Task PostMessageAsync(string webhookUrl, string text, CancellationToken cancellationToken)
        {
            PostedMessages.Add((webhookUrl, text));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSecretProtector : ISecretProtector
    {
        public string Protect(string plaintext) => $"protected({plaintext})";
        public string Unprotect(string protectedValue) => protectedValue[10..^1];
    }

    private sealed class FakeOAuthStateCodec : IOAuthStateCodec
    {
        public string Encode(string mode, DateTimeOffset now, TimeSpan lifetime, string? returnUrl = null) =>
            $"{mode}:{returnUrl}";

        public bool TryDecode(string state, DateTimeOffset now, out string mode, out string? returnUrl)
        {
            var parts = state.Split(':', 2);
            if (parts.Length != 2 || parts[0] != "slack-connect")
            {
                mode = string.Empty;
                returnUrl = null;
                return false;
            }

            mode = parts[0];
            returnUrl = parts[1];
            return true;
        }
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
