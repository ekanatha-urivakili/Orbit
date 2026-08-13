namespace Orbit.Domain.Choices;

public enum WorkItemType
{
    Initiative,
    Epic,
    Task,
    Story,
    Spike,
    Test,
    Feature,
    Request,
    Bug,
    Subtask
}

public enum WorkItemLinkType
{
    DependsOn,
    Blocks,
    RelatesTo
}

public enum WorkItemStatus
{
    Backlog,
    Selected,
    InProgress,
    InReview,
    Done,
    Blocked
}

public enum Priority
{
    Lowest,
    Low,
    Medium,
    High,
    Highest
}

public enum StatusCategory
{
    ToDo,
    InProgress,
    Done
}

public enum BoardType
{
    Scrum,
    Kanban
}

public enum SprintState
{
    Future,
    Active,
    Closing,
    Closed,
    Reopened
}

public enum EstimationMode
{
    StoryPoints,
    OriginalTime,
    ItemCount
}

public enum WipLimitMode
{
    Warn,
    Block
}

public enum ActorKind
{
    User,
    ServiceAccount,
    Automation,
    System
}

public enum OperationState
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Cancelled
}

public enum AgileFactType
{
    SprintAdded,
    SprintRemoved,
    EstimateChanged,
    StatusChanged,
    ColumnChanged,
    SprintCompleted,
    SprintReopened
}

public sealed record ChoiceOption<T>(
    string Id,
    T Value,
    string Label,
    string Description,
    int Order,
    string ColorToken,
    bool Enabled = true)
    where T : struct, Enum;

public static class SystemChoiceCatalog
{
    public static IReadOnlyList<ChoiceOption<WorkItemType>> WorkItemTypes { get; } =
    [
        new("initiative", WorkItemType.Initiative, "Initiative", "A strategic outcome containing epics.", 10, "lime"),
        new("epic", WorkItemType.Epic, "Epic", "A large outcome spanning multiple work items.", 20, "purple"),
        new("task", WorkItemType.Task, "Task", "A unit of implementation work.", 30, "blue"),
        new("story", WorkItemType.Story, "Story", "User-visible product value.", 40, "green"),
        new("bug", WorkItemType.Bug, "Bug", "A defect in expected behaviour.", 50, "red"),
        new("spike", WorkItemType.Spike, "Spike", "Time-boxed research that reduces uncertainty.", 60, "amber"),
        new("test", WorkItemType.Test, "Test", "A repeatable validation scenario.", 70, "teal"),
        new("feature", WorkItemType.Feature, "Feature", "A cohesive product capability.", 80, "cyan"),
        new("request", WorkItemType.Request, "Request", "A request from a customer or stakeholder.", 90, "orange"),
        new("subtask", WorkItemType.Subtask, "Subtask", "A historical child-work type.", 100, "slate", false)
    ];

    public static IReadOnlyList<ChoiceOption<WorkItemStatus>> WorkItemStatuses { get; } =
    [
        new("backlog", WorkItemStatus.Backlog, "Backlog", "Not yet selected for delivery.", 10, "slate"),
        new("selected", WorkItemStatus.Selected, "Selected", "Ready for the team to start.", 20, "cyan"),
        new("in-progress", WorkItemStatus.InProgress, "In progress", "Actively being worked on.", 30, "blue"),
        new("in-review", WorkItemStatus.InReview, "In review", "Awaiting review or validation.", 40, "amber"),
        new("done", WorkItemStatus.Done, "Done", "Meets the definition of done.", 50, "green"),
        new("blocked", WorkItemStatus.Blocked, "Blocked", "Cannot progress without intervention.", 60, "red")
    ];

    public static IReadOnlyList<ChoiceOption<Priority>> Priorities { get; } =
    [
        new("lowest", Priority.Lowest, "Lowest", "Can be scheduled after other work.", 10, "slate"),
        new("low", Priority.Low, "Low", "Below normal delivery priority.", 20, "sky"),
        new("medium", Priority.Medium, "Medium", "Normal delivery priority.", 30, "blue"),
        new("high", Priority.High, "High", "Important and should be scheduled soon.", 40, "orange"),
        new("highest", Priority.Highest, "Highest", "Requires immediate attention.", 50, "red")
    ];
}
