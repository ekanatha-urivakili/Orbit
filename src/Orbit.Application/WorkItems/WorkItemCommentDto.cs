using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

public sealed record WorkItemCommentDto(
    Guid Id,
    Guid WorkItemId,
    Guid AuthorMembershipId,
    string Body,
    Guid[] MentionedUserIds,
    bool IsDeleted,
    bool IsEdited,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastEditedAt)
{
    public static WorkItemCommentDto From(WorkItemComment comment) =>
        new(
            comment.Id,
            comment.WorkItemId,
            comment.AuthorMembershipId,
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
