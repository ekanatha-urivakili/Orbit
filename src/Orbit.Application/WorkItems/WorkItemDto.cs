using Orbit.Domain.Choices;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

public sealed record WorkItemDto(
    Guid Id,
    Guid ProjectId,
    string Key,
    string Summary,
    string? Description,
    Guid? ParentId,
    string? EpicName,
    string? AcceptanceCriteria,
    string? StepsToConduct,
    Guid? AssigneeUserId,
    Guid? DeveloperUserId,
    Guid? ProductOwnerUserId,
    string? SprintName,
    string? IdentifiedOn,
    decimal? StoryPoints,
    string[] Labels,
    string[] Countries,
    string[] AttachmentNames,
    WorkItemType Type,
    WorkItemStatus Status,
    Priority Priority,
    decimal Rank,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static WorkItemDto From(WorkItem workItem) =>
        new(
            workItem.Id,
            workItem.ProjectId,
            workItem.Key,
            workItem.Summary,
            workItem.Description,
            workItem.ParentId,
            workItem.EpicName,
            workItem.AcceptanceCriteria,
            workItem.StepsToConduct,
            workItem.AssigneeUserId,
            workItem.DeveloperUserId,
            workItem.ProductOwnerUserId,
            workItem.SprintName,
            workItem.IdentifiedOn,
            workItem.StoryPoints,
            workItem.Labels,
            workItem.Countries,
            workItem.AttachmentNames,
            workItem.Type,
            workItem.Status,
            workItem.Priority,
            workItem.Rank,
            workItem.Version,
            workItem.CreatedAt,
            workItem.UpdatedAt);
}
