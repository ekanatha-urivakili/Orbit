using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

// ---------------------------------------------------------------------------
// Mention extraction helper
// ---------------------------------------------------------------------------

internal static partial class MentionParser
{
    // Matches @{xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx} — UUID literal mentions.
    [GeneratedRegex(@"@\{([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\}",
        RegexOptions.Compiled)]
    private static partial Regex MentionRegex();

    /// <summary>
    /// Extracts all unique user-id UUIDs from @{guid} tokens in <paramref name="body"/>.
    /// Duplicates are removed; order is not guaranteed.
    /// </summary>
    public static Guid[] ExtractMentionedUserIds(string body)
    {
        var matches = MentionRegex().Matches(body);
        if (matches.Count == 0)
        {
            return [];
        }

        var ids = new HashSet<Guid>(matches.Count);
        foreach (Match match in matches)
        {
            if (Guid.TryParse(match.Groups[1].Value, out var userId))
            {
                ids.Add(userId);
            }
        }

        return [.. ids];
    }
}

// ---------------------------------------------------------------------------
// Add comment
// ---------------------------------------------------------------------------

public sealed record AddWorkItemCommentCommand(Guid WorkItemId, string Body) : ICommand<WorkItemCommentDto>;

public sealed class AddWorkItemCommentValidator : AbstractValidator<AddWorkItemCommentCommand>
{
    public AddWorkItemCommentValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.Body).NotEmpty().MaximumLength(10_000);
    }
}

public sealed class AddWorkItemCommentHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IWorkItemRepository workItems,
    IWorkItemCommentRepository comments,
    ISettingsRepository settings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<AddWorkItemCommentCommand, WorkItemCommentDto>
{
    public async Task<WorkItemCommentDto> Handle(
        AddWorkItemCommentCommand request,
        CancellationToken cancellationToken)
    {
        // Verify the work item exists and the caller can see it.
        _ = await workItems.GetAsync(
                tenantContext.TenantId,
                request.WorkItemId,
                ProjectPermission.View,
                cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        var mentionedUserIds = MentionParser.ExtractMentionedUserIds(request.Body);
        var comment = WorkItemComment.Create(
            tenantContext.TenantId,
            request.WorkItemId,
            principal.MembershipId,
            request.Body,
            mentionedUserIds,
            timeProvider.GetUtcNow());

        await comments.AddAsync(comment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var account = principal.UserId.HasValue
            ? await settings.GetUserAccountAsync(principal.UserId.Value, cancellationToken)
            : null;

        return WorkItemCommentDto.From(comment, account?.DisplayName, account?.AvatarUrl);
    }
}

// ---------------------------------------------------------------------------
// Edit comment
// ---------------------------------------------------------------------------

public sealed record EditWorkItemCommentCommand(
    Guid WorkItemId,
    Guid CommentId,
    string Body,
    long ExpectedVersion) : ICommand<WorkItemCommentDto>;

public sealed class EditWorkItemCommentValidator : AbstractValidator<EditWorkItemCommentCommand>
{
    public EditWorkItemCommentValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.CommentId).NotEmpty();
        RuleFor(command => command.Body).NotEmpty().MaximumLength(10_000);
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class EditWorkItemCommentHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IWorkItemCommentRepository comments,
    ISettingsRepository settings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<EditWorkItemCommentCommand, WorkItemCommentDto>
{
    public async Task<WorkItemCommentDto> Handle(
        EditWorkItemCommentCommand request,
        CancellationToken cancellationToken)
    {
        var comment = await comments.GetAsync(
                tenantContext.TenantId,
                request.WorkItemId,
                request.CommentId,
                cancellationToken)
            ?? throw new NotFoundException("Comment was not found.");

        if (comment.AuthorMembershipId != principal.MembershipId)
        {
            // Return 404 to avoid leaking existence of other users' comments.
            throw new NotFoundException("Comment was not found.");
        }

        if (comment.Version != request.ExpectedVersion)
        {
            throw new ConcurrencyException("The comment changed after it was loaded.");
        }

        comment.Edit(request.Body, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var account = principal.UserId.HasValue
            ? await settings.GetUserAccountAsync(principal.UserId.Value, cancellationToken)
            : null;

        return WorkItemCommentDto.From(comment, account?.DisplayName, account?.AvatarUrl);
    }
}

// ---------------------------------------------------------------------------
// Delete comment
// ---------------------------------------------------------------------------

public sealed record DeleteWorkItemCommentCommand(
    Guid WorkItemId,
    Guid CommentId,
    long ExpectedVersion) : ICommand<Unit>;

public sealed class DeleteWorkItemCommentValidator : AbstractValidator<DeleteWorkItemCommentCommand>
{
    public DeleteWorkItemCommentValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.CommentId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class DeleteWorkItemCommentHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IWorkItemCommentRepository comments,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<DeleteWorkItemCommentCommand, Unit>
{
    public async Task<Unit> Handle(
        DeleteWorkItemCommentCommand request,
        CancellationToken cancellationToken)
    {
        var comment = await comments.GetAsync(
                tenantContext.TenantId,
                request.WorkItemId,
                request.CommentId,
                cancellationToken)
            ?? throw new NotFoundException("Comment was not found.");

        if (comment.AuthorMembershipId != principal.MembershipId)
        {
            throw new NotFoundException("Comment was not found.");
        }

        if (comment.Version != request.ExpectedVersion)
        {
            throw new ConcurrencyException("The comment changed after it was loaded.");
        }

        comment.Delete(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

// ---------------------------------------------------------------------------
// List comments
// ---------------------------------------------------------------------------

public sealed record ListWorkItemCommentsQuery(Guid WorkItemId) : IQuery<IReadOnlyList<WorkItemCommentDto>>;

public sealed class ListWorkItemCommentsValidator : AbstractValidator<ListWorkItemCommentsQuery>
{
    public ListWorkItemCommentsValidator() => RuleFor(query => query.WorkItemId).NotEmpty();
}

public sealed class ListWorkItemCommentsHandler(
    ITenantContext tenantContext,
    IWorkItemRepository workItems,
    IWorkItemCommentRepository comments,
    ITenantMembershipRepository memberships,
    ISettingsRepository settings) : IRequestHandler<ListWorkItemCommentsQuery, IReadOnlyList<WorkItemCommentDto>>
{
    public async Task<IReadOnlyList<WorkItemCommentDto>> Handle(
        ListWorkItemCommentsQuery request,
        CancellationToken cancellationToken)
    {
        _ = await workItems.GetAsync(
                tenantContext.TenantId,
                request.WorkItemId,
                ProjectPermission.View,
                cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        var workItemComments = await comments.ListByWorkItemAsync(
            tenantContext.TenantId,
            request.WorkItemId,
            cancellationToken);

        var tenantMembers = (await memberships.ListAsync(tenantContext.TenantId, cancellationToken))
            .ToDictionary(m => m.Id);

        var userIds = tenantMembers.Values
            .Where(m => m.UserId.HasValue)
            .Select(m => m.UserId!.Value)
            .Distinct()
            .ToArray();

        var accounts = (await settings.GetUserAccountsAsync(userIds, cancellationToken))
            .ToDictionary(a => a.Id);

        return workItemComments.Select(c =>
        {
            string? displayName = null;
            string? avatarUrl = null;
            if (tenantMembers.TryGetValue(c.AuthorMembershipId, out var member))
            {
                if (member.UserId.HasValue && accounts.TryGetValue(member.UserId.Value, out var account))
                {
                    displayName = account.DisplayName;
                    avatarUrl = account.AvatarUrl;
                }
                else
                {
                    displayName = member.Subject;
                }
            }

            return WorkItemCommentDto.From(c, displayName, avatarUrl);
        }).ToArray();
    }
}
