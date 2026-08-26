namespace ScrumPulse.Domain.Events;

using ScrumPulse.Domain.Enums;

public record WorkItemCompletedEvent(
    Guid WorkItemId,
    string WorkItemKey,
    string Title,
    int StoryPoints,
    Guid? SprintId,
    Guid? AssigneeId,
    double? TotalCycleTimeHours,
    bool IsEscapedDefect,
    Guid EventId,
    DateTime OccurredAtUtc
) : IDomainEvent
{
    public WorkItemCompletedEvent(Guid workItemId, string workItemKey, string title, int storyPoints, Guid? sprintId, Guid? assigneeId, double? totalCycleTimeHours, bool isEscapedDefect)
        : this(workItemId, workItemKey, title, storyPoints, sprintId, assigneeId, totalCycleTimeHours, isEscapedDefect, Guid.NewGuid(), DateTime.UtcNow) { }
}

public record WorkItemStageAdvancedEvent(
    Guid WorkItemId,
    string WorkItemKey,
    WorkItemStatus PreviousStatus,
    WorkItemStatus NewStatus,
    Guid? AssigneeId,
    Guid EventId,
    DateTime OccurredAtUtc
) : IDomainEvent
{
    public WorkItemStageAdvancedEvent(Guid workItemId, string workItemKey, WorkItemStatus previousStatus, WorkItemStatus newStatus, Guid? assigneeId)
        : this(workItemId, workItemKey, previousStatus, newStatus, assigneeId, Guid.NewGuid(), DateTime.UtcNow) { }
}

public record BlockerRaisedEvent(
    Guid BlockerId,
    string Title,
    BlockerCategory Category,
    int SlaHoursLimit,
    Guid? SprintId,
    Guid? RaisedById,
    Guid EventId,
    DateTime OccurredAtUtc
) : IDomainEvent
{
    public BlockerRaisedEvent(Guid blockerId, string title, BlockerCategory category, int slaHoursLimit, Guid? sprintId, Guid? raisedById)
        : this(blockerId, title, category, slaHoursLimit, sprintId, raisedById, Guid.NewGuid(), DateTime.UtcNow) { }
}

public record BlockerResolvedEvent(
    Guid BlockerId,
    string Title,
    string ResolutionNotes,
    double HoursWaiting,
    bool WasSlaBreached,
    Guid EventId,
    DateTime OccurredAtUtc
) : IDomainEvent
{
    public BlockerResolvedEvent(Guid blockerId, string title, string resolutionNotes, double hoursWaiting, bool wasSlaBreached)
        : this(blockerId, title, resolutionNotes, hoursWaiting, wasSlaBreached, Guid.NewGuid(), DateTime.UtcNow) { }
}

public record KudosSentEvent(
    Guid KudosId,
    Guid SenderId,
    Guid ReceiverId,
    BadgeType Badge,
    string Message,
    Guid EventId,
    DateTime OccurredAtUtc
) : IDomainEvent
{
    public KudosSentEvent(Guid kudosId, Guid senderId, Guid receiverId, BadgeType badge, string message)
        : this(kudosId, senderId, receiverId, badge, message, Guid.NewGuid(), DateTime.UtcNow) { }
}
