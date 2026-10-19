namespace ScrumPulse.Application.Integration;

using ScrumPulse.Domain.Enums;

/// <summary>One state change recorded by the board, as read from its revision history.</summary>
public readonly record struct BoardStateChange(string BoardState, DateTime ChangedAtUtc);

/// <summary>
/// Stage timestamps derived from a board's revision history. Each is the moment the item
/// <em>first</em> reached that stage; null means it never did.
/// </summary>
public sealed class StageTimeline
{
    public DateTime? PickedUpAtUtc { get; init; }
    public DateTime? PrCreatedAtUtc { get; init; }
    public DateTime? PrApprovedAtUtc { get; init; }
    public DateTime? PrMergedAtUtc { get; init; }
    public DateTime? QaStartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }

    /// <summary>Board states encountered that the mapping did not cover.</summary>
    public IReadOnlyList<string> UnmappedStates { get; init; } = [];
}

/// <summary>
/// Turns a board's state transition history into the stage timestamps ScrumPulse measures. The
/// <em>first</em> entry into a stage is kept rather than the last, so an item bounced back from
/// review shows its full elapsed time instead of a flattering restart. Skipped stages stay null.
/// </summary>
public static class StageTimelineBuilder
{
    public static StageTimeline Build(
        IEnumerable<BoardStateChange> stateChanges,
        BoardStateMapping mapping)
    {
        // Ordered because revision APIs do not guarantee it.
        var orderedChanges = stateChanges
            .Where(change => !string.IsNullOrWhiteSpace(change.BoardState))
            .OrderBy(change => change.ChangedAtUtc)
            .ToList();

        var firstEntryByStage = new Dictionary<WorkItemStatus, DateTime>();
        var observedStates = new List<string>(orderedChanges.Count);

        foreach (var change in orderedChanges)
        {
            observedStates.Add(change.BoardState);

            var stage = mapping.Resolve(change.BoardState);
            if (stage is null)
            {
                continue;
            }

            // TryAdd keeps the earliest: a later revisit of the same stage is ignored.
            firstEntryByStage.TryAdd(stage.Value, change.ChangedAtUtc);
        }

        return new StageTimeline
        {
            PickedUpAtUtc = Lookup(firstEntryByStage, WorkItemStatus.InProgress),
            PrCreatedAtUtc = Lookup(firstEntryByStage, WorkItemStatus.PrCreated),
            PrApprovedAtUtc = Lookup(firstEntryByStage, WorkItemStatus.PrApproved),
            PrMergedAtUtc = Lookup(firstEntryByStage, WorkItemStatus.Merged),
            QaStartedAtUtc = Lookup(firstEntryByStage, WorkItemStatus.InQa),
            CompletedAtUtc = Lookup(firstEntryByStage, WorkItemStatus.Done),
            UnmappedStates = mapping.FindUnmappedStates(observedStates)
        };
    }

    private static DateTime? Lookup(
        Dictionary<WorkItemStatus, DateTime> firstEntryByStage,
        WorkItemStatus stage) =>
        firstEntryByStage.TryGetValue(stage, out var timestamp) ? timestamp : null;
}
