using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Integrations;

namespace Orbit.Application.Integrations;

public sealed record SlackConnectionDto(
    Guid Id, Guid ProjectId, string TeamName, string ChannelName, DateTimeOffset CreatedAt);

// ------------------------------------------------------------------
// Start: build the Slack authorize URL, carrying the project id through
// the signed OAuth `state` so the callback can resolve it without a
// server-side session.
// ------------------------------------------------------------------

public sealed record StartSlackConnectCommand(Guid ProjectId) : ICommand<string>;

public sealed class StartSlackConnectValidator : AbstractValidator<StartSlackConnectCommand>
{
    public StartSlackConnectValidator() => RuleFor(command => command.ProjectId).NotEmpty();
}

public sealed class StartSlackConnectHandler(
    ITenantContext tenantContext,
    IProjectRepository projects,
    ISlackClient slackClient,
    IOAuthStateCodec stateCodec,
    TimeProvider timeProvider) : IRequestHandler<StartSlackConnectCommand, string>
{
    public async Task<string> Handle(StartSlackConnectCommand request, CancellationToken cancellationToken)
    {
        _ = await projects.GetAsync(
                tenantContext.TenantId, request.ProjectId, ProjectPermission.Administer, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");

        var state = stateCodec.Encode(
            "slack-connect", timeProvider.GetUtcNow(), TimeSpan.FromMinutes(10), request.ProjectId.ToString());
        return slackClient.BuildAuthorizeUrl(state);
    }
}

// ------------------------------------------------------------------
// Complete: exchange the code, persist the connection (replacing any
// prior one for the project).
// ------------------------------------------------------------------

public sealed record CompleteSlackOAuthCommand(string Code, string State) : ICommand<SlackConnectionDto>;

public sealed class CompleteSlackOAuthValidator : AbstractValidator<CompleteSlackOAuthCommand>
{
    public CompleteSlackOAuthValidator()
    {
        RuleFor(command => command.Code).NotEmpty();
        RuleFor(command => command.State).NotEmpty();
    }
}

public sealed class CompleteSlackOAuthHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IProjectRepository projects,
    ISlackConnectionRepository connections,
    ISlackClient slackClient,
    IOAuthStateCodec stateCodec,
    ISecretProtector secretProtector,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<CompleteSlackOAuthCommand, SlackConnectionDto>
{
    public async Task<SlackConnectionDto> Handle(
        CompleteSlackOAuthCommand request, CancellationToken cancellationToken)
    {
        if (!stateCodec.TryDecode(request.State, timeProvider.GetUtcNow(), out var mode, out var returnUrl)
            || mode != "slack-connect"
            || returnUrl is null
            || !Guid.TryParse(returnUrl, out var projectId))
        {
            throw new ValidationException("The Slack connection request expired or is invalid. Try again.");
        }

        var project = await projects.GetAsync(
            tenantContext.TenantId, projectId, ProjectPermission.Administer, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");

        var webhook = await slackClient.ExchangeCodeAsync(request.Code, cancellationToken);

        var existing = await connections.GetByProjectAsync(tenantContext.TenantId, project.Id, cancellationToken);
        if (existing is not null)
        {
            await connections.RemoveAsync(existing, cancellationToken);
        }

        var userId = PrincipalGuards.RequireUser(principal);
        var connection = SlackConnection.Create(
            tenantContext.TenantId,
            project.Id,
            webhook.TeamId,
            webhook.TeamName,
            webhook.ChannelId,
            webhook.ChannelName,
            secretProtector.Protect(webhook.WebhookUrl),
            userId,
            timeProvider.GetUtcNow());

        await connections.AddAsync(connection, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new SlackConnectionDto(connection.Id, connection.ProjectId, connection.TeamName, connection.ChannelName, connection.CreatedAt);
    }
}

// ------------------------------------------------------------------
// Query current connection / disconnect
// ------------------------------------------------------------------

public sealed record GetSlackConnectionQuery(Guid ProjectId) : IQuery<SlackConnectionDto?>;

public sealed class GetSlackConnectionValidator : AbstractValidator<GetSlackConnectionQuery>
{
    public GetSlackConnectionValidator() => RuleFor(query => query.ProjectId).NotEmpty();
}

public sealed class GetSlackConnectionHandler(
    ITenantContext tenantContext,
    IProjectRepository projects,
    ISlackConnectionRepository connections) : IRequestHandler<GetSlackConnectionQuery, SlackConnectionDto?>
{
    public async Task<SlackConnectionDto?> Handle(GetSlackConnectionQuery request, CancellationToken cancellationToken)
    {
        _ = await projects.GetAsync(
                tenantContext.TenantId, request.ProjectId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");

        var connection = await connections.GetByProjectAsync(tenantContext.TenantId, request.ProjectId, cancellationToken);
        return connection is null
            ? null
            : new SlackConnectionDto(connection.Id, connection.ProjectId, connection.TeamName, connection.ChannelName, connection.CreatedAt);
    }
}

public sealed record DisconnectSlackCommand(Guid ConnectionId) : ICommand<Unit>;

public sealed class DisconnectSlackValidator : AbstractValidator<DisconnectSlackCommand>
{
    public DisconnectSlackValidator() => RuleFor(command => command.ConnectionId).NotEmpty();
}

public sealed class DisconnectSlackHandler(
    ITenantContext tenantContext,
    IProjectRepository projects,
    ISlackConnectionRepository connections,
    IUnitOfWork unitOfWork) : IRequestHandler<DisconnectSlackCommand, Unit>
{
    public async Task<Unit> Handle(DisconnectSlackCommand request, CancellationToken cancellationToken)
    {
        var connection = await connections.GetAsync(tenantContext.TenantId, request.ConnectionId, cancellationToken)
            ?? throw new NotFoundException("Slack connection was not found.");

        _ = await projects.GetAsync(
                tenantContext.TenantId, connection.ProjectId, ProjectPermission.Administer, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");

        await connections.RemoveAsync(connection, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

// ------------------------------------------------------------------
// Post a work item summary to the connected channel ("Share in Slack")
// ------------------------------------------------------------------

public sealed record PostWorkItemToSlackCommand(Guid WorkItemId, string? Message) : ICommand<Unit>;

public sealed class PostWorkItemToSlackValidator : AbstractValidator<PostWorkItemToSlackCommand>
{
    public PostWorkItemToSlackValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.Message).MaximumLength(2_000);
    }
}

public sealed class PostWorkItemToSlackHandler(
    ITenantContext tenantContext,
    IWorkItemRepository workItems,
    ISlackConnectionRepository connections,
    ISlackClient slackClient,
    ISecretProtector secretProtector) : IRequestHandler<PostWorkItemToSlackCommand, Unit>
{
    public async Task<Unit> Handle(PostWorkItemToSlackCommand request, CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetAsync(
            tenantContext.TenantId, request.WorkItemId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        var connection = await connections.GetByProjectAsync(tenantContext.TenantId, workItem.ProjectId, cancellationToken)
            ?? throw new ValidationException("This project has no connected Slack channel yet.");

        var text = string.IsNullOrWhiteSpace(request.Message)
            ? $"*{workItem.Key}*: {workItem.Summary}\n/browse/{workItem.Key}"
            : $"*{workItem.Key}*: {workItem.Summary}\n{request.Message}\n/browse/{workItem.Key}";

        await slackClient.PostMessageAsync(secretProtector.Unprotect(connection.EncryptedWebhookUrl), text, cancellationToken);
        return Unit.Value;
    }
}
