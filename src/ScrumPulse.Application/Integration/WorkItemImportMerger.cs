namespace ScrumPulse.Application.Integration;

using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;

/// <summary>
/// Applies an imported work item onto the stored row. Three rules govern it: a manually created
/// item is never touched, free text crosses only as far as the content policy allows, and a file
/// import may fill gaps but never erase.
/// </summary>
public static class WorkItemImportMerger
{
    /// <summary>Why an item was left alone, for reporting rather than silent skipping.</summary>
    public enum MergeOutcome
    {
        Created,

        Updated,

        SkippedManualItem
    }

    /// <param name="utcNow">Injected so the sync timestamp is deterministic under test.</param>
    public static MergeOutcome Merge(
        WorkItem target,
        ImportedWorkItem imported,
        WorkItemOrigin origin,
        ContentSensitivityPolicy contentPolicy,
        bool isNewItem,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(imported);

        // Hand-typed items belong to whoever typed them. An unrelated board reusing the same
        // identifier must not destroy that work, so the import yields.
        if (!isNewItem && target.Origin == WorkItemOrigin.Manual)
        {
            return MergeOutcome.SkippedManualItem;
        }

        target.Origin = origin;
        target.ExternalId = imported.ExternalId;
        target.Key = imported.Key;
        target.Type = imported.Type;
        target.Status = imported.Status;
        target.Priority = imported.Priority;
        target.StoryPoints = imported.StoryPoints;
        target.BoardState = imported.BoardState;
        target.LastSyncedAtUtc = utcNow;

        ApplyContent(target, imported, contentPolicy);

        ApplyTimeline(target, imported.Timeline, canClearExistingValues: origin == WorkItemOrigin.AzureDevOps);

        // Keeping the row's own insertion time would make pickup latency measure how long ago
        // the sync ran.
        if (imported.CreatedAtUtc != default)
        {
            target.CreatedAtUtc = imported.CreatedAtUtc;
        }

        return isNewItem ? MergeOutcome.Created : MergeOutcome.Updated;
    }

    /// <summary>Copies as much free text as the policy allows, and no more.</summary>
    private static void ApplyContent(
        WorkItem target,
        ImportedWorkItem imported,
        ContentSensitivityPolicy contentPolicy)
    {
        switch (contentPolicy)
        {
            case ContentSensitivityPolicy.MetricsOnly:
                target.Title = imported.Key;
                target.Description = string.Empty;

                // The board URL names the client's organisation and project.
                target.ExternalUrl = null;
                break;

            case ContentSensitivityPolicy.RedactedTitles:
                target.Title = SensitiveContentRedactor.Redact(imported.Title, MaximumTitleLength) ?? imported.Key;
                target.Description = string.Empty;
                target.ExternalUrl = imported.ExternalUrl;
                break;

            case ContentSensitivityPolicy.RedactedTitlesAndDescriptions:
                target.Title = SensitiveContentRedactor.Redact(imported.Title, MaximumTitleLength) ?? imported.Key;
                target.Description = SensitiveContentRedactor.Redact(imported.Description) ?? string.Empty;
                target.ExternalUrl = imported.ExternalUrl;
                break;

            default:
                // The enum grew and this switch did not. Strictest option wins.
                target.Title = imported.Key;
                target.Description = string.Empty;
                target.ExternalUrl = null;
                break;
        }

        // Keeps a long board title from failing the whole sync on a column constraint.
        if (target.Title.Length > MaximumTitleLength)
        {
            target.Title = target.Title[..MaximumTitleLength];
        }
    }

    /// <summary>Matches the Title column length in the persistence configuration.</summary>
    private const int MaximumTitleLength = 300;

    /// <param name="canClearExistingValues">
    /// True for a live sync, whose history is complete enough that a null means the stage was
    /// never reached. False for a file import, where absence means "not exported" rather than
    /// "did not happen" and would otherwise wipe timestamps an earlier sync established.
    /// </param>
    private static void ApplyTimeline(WorkItem target, StageTimeline timeline, bool canClearExistingValues)
    {
        target.PickedUpAtUtc = Choose(target.PickedUpAtUtc, timeline.PickedUpAtUtc, canClearExistingValues);
        target.PrCreatedAtUtc = Choose(target.PrCreatedAtUtc, timeline.PrCreatedAtUtc, canClearExistingValues);
        target.PrApprovedAtUtc = Choose(target.PrApprovedAtUtc, timeline.PrApprovedAtUtc, canClearExistingValues);
        target.PrMergedAtUtc = Choose(target.PrMergedAtUtc, timeline.PrMergedAtUtc, canClearExistingValues);
        target.QaStartedAtUtc = Choose(target.QaStartedAtUtc, timeline.QaStartedAtUtc, canClearExistingValues);
        target.CompletedAtUtc = Choose(target.CompletedAtUtc, timeline.CompletedAtUtc, canClearExistingValues);
    }

    private static DateTime? Choose(DateTime? existing, DateTime? incoming, bool canClearExistingValues) =>
        canClearExistingValues ? incoming : incoming ?? existing;
}
