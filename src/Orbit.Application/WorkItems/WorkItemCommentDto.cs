using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

public sealed record WorkItemCommentDto(
    Guid Id,
    Guid WorkItemId,
    Guid AuthorMembershipId,
    string? AuthorDisplayName,
    string? AuthorAvatarUrl,
    string Body,
    Guid[] MentionedUserIds,
    bool IsDeleted,
    bool IsEdited,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastEditedAt)
{
    public static WorkItemCommentDto From(
        WorkItemComment comment,
        string? authorDisplayName = null,
        string? authorAvatarUrl = null) =>
        new(
            comment.Id,
            comment.WorkItemId,
            comment.AuthorMembershipId,
            authorDisplayName ?? "Unknown member",
            authorAvatarUrl,
            // Mask body on soft-deleted comments — consistent with GitHub / Jira behaviour.
            comment.IsDeleted ? "[deleted]" : comment.Body,
            comment.IsDeleted ? [] : comment.MentionedUserIds,
            comment.IsDeleted,
            comment.LastEditedAt.HasValue,
            comment.Version,
            comment.CreatedAt,
            comment.UpdatedAt,
            comment.LastEditedAt);
}
