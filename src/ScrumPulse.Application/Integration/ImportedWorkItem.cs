namespace ScrumPulse.Application.Integration;

using ScrumPulse.Domain.Enums;

// The single contract both import routes produce, so sync and file import cannot drift apart.
public sealed class ImportedWorkItem
{
    /// <summary>Identifier on the source board. Used to match on re-import.</summary>
    public required string ExternalId { get; init; }

    public required string Key { get; init; }

    public required string Title { get; init; }

    public string Description { get; init; } = string.Empty;

    public string? ExternalUrl { get; init; }

    public WorkItemType Type { get; init; } = WorkItemType.UserStory;

    /// <summary>Resolved through the board state mapping.</summary>
    public WorkItemStatus Status { get; init; } = WorkItemStatus.Backlog;

    public PriorityLevel Priority { get; init; } = PriorityLevel.Medium;

    public int StoryPoints { get; init; }

    public string? AssignedToDisplayName { get; init; }

    public string? IterationPath { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    // Sparse on the file-import path, where there is no revision history to read.
    public StageTimeline Timeline { get; init; } = new();

    /// <summary>Retained so an unmapped column can be reported by name.</summary>
    public string? BoardState { get; init; }
}
