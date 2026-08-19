using Orbit.Domain.Choices;
using Orbit.Domain.Common;

namespace Orbit.Domain.WorkItems;

public sealed class WorkItem
{
    private WorkItem()
    {
    }

    private WorkItem(
        Guid id,
        Guid tenantId,
        Guid projectId,
        long sequenceNumber,
        string projectKey,
        string summary,
        string? description,
        WorkItemType type,
        Priority priority,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        SequenceNumber = sequenceNumber;
        Key = $"{projectKey}-{sequenceNumber}";
        Summary = summary;
        Description = description;
        Type = type;
        Priority = priority;
        Status = WorkItemStatus.Backlog;
        Rank = sequenceNumber * 1024m;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public long SequenceNumber { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid? ParentId { get; private set; }
    public string? EpicName { get; private set; }
    public string? AcceptanceCriteria { get; private set; }
    public string? StepsToConduct { get; private set; }
    public Guid? AssigneeUserId { get; private set; }
    public Guid? DeveloperUserId { get; private set; }
    public Guid? ProductOwnerUserId { get; private set; }
    public string? SprintName { get; private set; }
    public string? IdentifiedOn { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public Guid? TeamId { get; private set; }
    public decimal? StoryPoints { get; private set; }
    public string[] Labels { get; private set; } = [];
    public string[] Countries { get; private set; } = [];
    public string[] AttachmentNames { get; private set; } = [];
    public WorkItemType Type { get; private set; }
    public WorkItemStatus Status { get; private set; }
    public Priority Priority { get; private set; }
    public decimal Rank { get; private set; }
    public bool IsFlagged { get; private set; }
    public Guid? CoverAttachmentId { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static WorkItem Create(
        Guid tenantId,
        Guid projectId,
        long sequenceNumber,
        string projectKey,
        string summary,
        string? description,
        WorkItemType type,
        Priority priority,
        DateTimeOffset now)
    {
        var normalizedSummary = summary.Trim();
        if (normalizedSummary.Length is < 3 or > 255)
        {
            throw new DomainException("Summary must contain 3 to 255 characters.");
        }

        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (normalizedDescription?.Length > 32_000)
        {
            throw new DomainException("Description cannot exceed 32,000 characters.");
        }

        return new WorkItem(
            Guid.CreateVersion7(),
            tenantId,
            projectId,
            sequenceNumber,
            projectKey,
            normalizedSummary,
            normalizedDescription,
            type,
            priority,
            now);
    }

    public void SetDetails(
        Guid? parentId,
        string? epicName,
        string? acceptanceCriteria,
        string? stepsToConduct,
        Guid? assigneeUserId,
        Guid? developerUserId,
        Guid? productOwnerUserId,
        string? sprintName,
        string? identifiedOn,
        DateOnly? startDate,
        Guid? teamId,
        decimal? storyPoints,
        IEnumerable<string>? labels,
        IEnumerable<string>? countries,
        IEnumerable<string>? attachmentNames)
    {
        if (Type == WorkItemType.Epic && string.IsNullOrWhiteSpace(epicName))
        {
            throw new DomainException("Epic name is required for an epic.");
        }

        if (storyPoints is < 0 or > 10_000)
        {
            throw new DomainException("Story points must be between 0 and 10,000.");
        }

        if (parentId == Id)
        {
            throw new DomainException("A work item cannot reference itself.");
        }

        ParentId = parentId;
        EpicName = Normalize(epicName, 255, "Epic name");
        AcceptanceCriteria = Normalize(acceptanceCriteria, 32_000, "Acceptance criteria");
        StepsToConduct = Normalize(stepsToConduct, 32_000, "Steps to conduct");
        AssigneeUserId = assigneeUserId;
        DeveloperUserId = developerUserId;
        ProductOwnerUserId = productOwnerUserId;
        SprintName = Normalize(sprintName, 255, "Sprint");
        IdentifiedOn = Normalize(identifiedOn, 255, "Identified on");
        StartDate = startDate;
        TeamId = teamId;
        StoryPoints = storyPoints;
        Labels = NormalizeValues(labels, 50, 100, "Label");
        Countries = NormalizeValues(countries, 50, 100, "Country");
        AttachmentNames = NormalizeValues(attachmentNames, 20, 255, "Attachment name");
    }

    private static string? Normalize(string? value, int maxLength, string field)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maxLength)
        {
            throw new DomainException($"{field} cannot exceed {maxLength:N0} characters.");
        }

        return normalized;
    }

    private static string[] NormalizeValues(
        IEnumerable<string>? values,
        int maximumCount,
        int maximumLength,
        string field)
    {
        var normalized = values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        if (normalized.Length > maximumCount || normalized.Any(value => value.Length > maximumLength))
        {
            throw new DomainException($"{field} values exceed the supported limits.");
        }

        return normalized;
    }

    public void Update(
        string summary,
        string? description,
        Priority priority,
        Guid? parentId,
        string? epicName,
        string? acceptanceCriteria,
        string? stepsToConduct,
        Guid? assigneeUserId,
        Guid? developerUserId,
        Guid? productOwnerUserId,
        string? sprintName,
        string? identifiedOn,
        DateOnly? startDate,
        Guid? teamId,
        decimal? storyPoints,
        IEnumerable<string>? labels,
        IEnumerable<string>? countries,
        IEnumerable<string>? attachmentNames,
        DateTimeOffset now)
    {
        var normalizedSummary = summary.Trim();
        if (normalizedSummary.Length is < 3 or > 255)
        {
            throw new DomainException("Summary must contain 3 to 255 characters.");
        }

        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (normalizedDescription?.Length > 32_000)
        {
            throw new DomainException("Description cannot exceed 32,000 characters.");
        }

        Summary = normalizedSummary;
        Description = normalizedDescription;
        Priority = priority;
        SetDetails(
            parentId,
            epicName,
            acceptanceCriteria,
            stepsToConduct,
            assigneeUserId,
            developerUserId,
            productOwnerUserId,
            sprintName,
            identifiedOn,
            startDate,
            teamId,
            storyPoints,
            labels,
            countries,
            attachmentNames);

        Version++;
        UpdatedAt = now;
    }

    public void ChangeStatus(WorkItemStatus status, DateTimeOffset now)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;
        Version++;
        UpdatedAt = now;
    }

    public void Reorder(decimal rank, DateTimeOffset now)
    {
        if (Rank == rank)
        {
            return;
        }

        Rank = rank;
        Version++;
        UpdatedAt = now;
    }

    public void ChangeType(WorkItemType newType, DateTimeOffset now)
    {
        if (Type == newType)
        {
            return;
        }

        if (Type is WorkItemType.Initiative or WorkItemType.Epic or WorkItemType.Subtask
            || newType is WorkItemType.Initiative or WorkItemType.Epic or WorkItemType.Subtask)
        {
            throw new DomainException(
                "Initiative, Epic and Subtask work items cannot be converted to or from another type.");
        }

        Type = newType;
        Version++;
        UpdatedAt = now;
    }

    public void SetFlagged(bool flagged, DateTimeOffset now)
    {
        if (IsFlagged == flagged)
        {
            return;
        }

        IsFlagged = flagged;
        Version++;
        UpdatedAt = now;
    }

    public void SetCover(Guid? attachmentId, DateTimeOffset now)
    {
        if (CoverAttachmentId == attachmentId)
        {
            return;
        }

        CoverAttachmentId = attachmentId;
        Version++;
        UpdatedAt = now;
    }

    public void Archive(DateTimeOffset now)
    {
        if (IsArchived)
        {
            return;
        }

        IsArchived = true;
        ArchivedAt = now;
        Version++;
        UpdatedAt = now;
    }

    public void Unarchive(DateTimeOffset now)
    {
        if (!IsArchived)
        {
            return;
        }

        IsArchived = false;
        ArchivedAt = null;
        Version++;
        UpdatedAt = now;
    }

    public void MoveToProject(Guid projectId, long sequenceNumber, string projectKey, DateTimeOffset now)
    {
        ProjectId = projectId;
        SequenceNumber = sequenceNumber;
        Key = $"{projectKey}-{sequenceNumber}";
        Version++;
        UpdatedAt = now;
    }
}
