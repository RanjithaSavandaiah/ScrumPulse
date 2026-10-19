namespace ScrumPulse.Tests.Integration;

using ScrumPulse.Application.Integration;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using Xunit;

/// <summary>
/// Covers what an import is permitted to write over.
/// </summary>
/// <remarks>
/// <para>
/// None of these rules fail loudly. A merge that overwrites the wrong field does not throw and
/// does not log; it changes a stored number, and the first anyone hears of it is a delivery
/// lead asking why a cycle time halved overnight with no way to recover the original. That is
/// why the suite asserts on the stored row rather than on a return value.
/// </para>
/// <para>
/// Three guarantees are under test. A hand-created item is never touched by a board. Client
/// free text crosses only as far as the content policy allows, and an unrecognised policy is
/// treated as the strictest rather than the loosest. And a file import may fill a gap but
/// never erase one, because a CSV carries no history and its nulls mean "not exported".
/// </para>
/// </remarks>
public class WorkItemImportMergerTests
{
    private static readonly DateTime SyncedAt = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime BoardCreatedAt = new(2026, 9, 14, 8, 30, 0, DateTimeKind.Utc);

    private static ImportedWorkItem Imported(
        string externalId = "4182",
        string? title = "Fix the checkout totals for Contoso",
        string description = "Repro steps live in the ticket.",
        string? externalUrl = "https://dev.azure.com/contoso/_workitems/edit/4182",
        DateTime createdAtUtc = default,
        StageTimeline? timeline = null) =>
        new()
        {
            ExternalId = externalId,
            Key = $"AB#{externalId}",
            Title = title ?? $"AB#{externalId}",
            Description = description,
            ExternalUrl = externalUrl,
            Type = WorkItemType.Bug,
            Status = WorkItemStatus.InProgress,
            Priority = PriorityLevel.High,
            StoryPoints = 5,
            CreatedAtUtc = createdAtUtc,
            BoardState = "Active",
            Timeline = timeline ?? new StageTimeline()
        };

    [Fact]
    public void Merge_LeavesAManuallyCreatedItemUntouched()
    {
        // The destructive case. Two boards can reuse the same identifier, and an import that
        // claimed a hand-typed item would overwrite somebody's work with no warning and no
        // undo. Yielding costs an unmatched item; overwriting costs the item itself.
        var stored = new WorkItem
        {
            Origin = WorkItemOrigin.Manual,
            Key = "SP-9",
            Title = "Written here by the Scrum Master",
            StoryPoints = 13
        };

        var outcome = WorkItemImportMerger.Merge(
            stored,
            Imported(),
            WorkItemOrigin.AzureDevOps,
            ContentSensitivityPolicy.RedactedTitles,
            isNewItem: false,
            SyncedAt);

        Assert.Equal(WorkItemImportMerger.MergeOutcome.SkippedManualItem, outcome);
        Assert.Equal("SP-9", stored.Key);
        Assert.Equal("Written here by the Scrum Master", stored.Title);
        Assert.Equal(13, stored.StoryPoints);
        Assert.Equal(WorkItemOrigin.Manual, stored.Origin);
    }

    [Fact]
    public void Merge_ReportsCreated_ForAnItemTheBoardHasNotSentBefore()
    {
        var stored = new WorkItem();

        var outcome = WorkItemImportMerger.Merge(
            stored,
            Imported(),
            WorkItemOrigin.AzureDevOps,
            ContentSensitivityPolicy.MetricsOnly,
            isNewItem: true,
            SyncedAt);

        Assert.Equal(WorkItemImportMerger.MergeOutcome.Created, outcome);
        Assert.Equal("4182", stored.ExternalId);
        Assert.Equal(WorkItemOrigin.AzureDevOps, stored.Origin);
        Assert.Equal(SyncedAt, stored.LastSyncedAtUtc);
    }

    [Fact]
    public void Merge_ReportsUpdated_AndRefreshesAnItemTheBoardAlreadyOwns()
    {
        var stored = new WorkItem
        {
            Origin = WorkItemOrigin.AzureDevOps,
            ExternalId = "4182",
            Status = WorkItemStatus.Backlog,
            StoryPoints = 2
        };

        var outcome = WorkItemImportMerger.Merge(
            stored,
            Imported(),
            WorkItemOrigin.AzureDevOps,
            ContentSensitivityPolicy.MetricsOnly,
            isNewItem: false,
            SyncedAt);

        Assert.Equal(WorkItemImportMerger.MergeOutcome.Updated, outcome);
        Assert.Equal(WorkItemStatus.InProgress, stored.Status);
        Assert.Equal(5, stored.StoryPoints);
        Assert.Equal("Active", stored.BoardState);
    }

    [Fact]
    public void Merge_UnderMetricsOnly_StoresTheReferenceAndNoClientText()
    {
        // The default, and the only setting that is safe without reading every work item on
        // the client's board first. The URL goes too: it names the organisation and project.
        var stored = new WorkItem();

        WorkItemImportMerger.Merge(
            stored,
            Imported(),
            WorkItemOrigin.AzureDevOps,
            ContentSensitivityPolicy.MetricsOnly,
            isNewItem: true,
            SyncedAt);

        Assert.Equal("AB#4182", stored.Title);
        Assert.Equal(string.Empty, stored.Description);
        Assert.Null(stored.ExternalUrl);
    }

    [Fact]
    public void Merge_UnderRedactedTitles_KeepsATitleButStillDropsTheDescription()
    {
        var stored = new WorkItem();

        WorkItemImportMerger.Merge(
            stored,
            Imported(title: "Checkout totals wrong for large baskets"),
            WorkItemOrigin.AzureDevOps,
            ContentSensitivityPolicy.RedactedTitles,
            isNewItem: true,
            SyncedAt);

        Assert.Equal("Checkout totals wrong for large baskets", stored.Title);
        Assert.Equal(string.Empty, stored.Description);
        Assert.Equal("https://dev.azure.com/contoso/_workitems/edit/4182", stored.ExternalUrl);
    }

    [Fact]
    public void Merge_UnderRedactedTitlesAndDescriptions_CarriesBothThroughTheRedactor()
    {
        var stored = new WorkItem();

        WorkItemImportMerger.Merge(
            stored,
            Imported(
                title: "Basket total is out by a penny",
                description: "Seen on build 4.2; the rounding runs before VAT."),
            WorkItemOrigin.AzureDevOps,
            ContentSensitivityPolicy.RedactedTitlesAndDescriptions,
            isNewItem: true,
            SyncedAt);

        Assert.Equal("Basket total is out by a penny", stored.Title);
        Assert.Contains("rounding runs before VAT", stored.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Merge_FallsBackToTheStrictestPolicy_WhenTheEnumValueIsUnrecognised()
    {
        // The enum will grow before this switch does. A default that leaked client text would
        // turn a routine addition into a disclosure, so the unknown case has to behave like
        // MetricsOnly rather than like the most permissive branch.
        var stored = new WorkItem();

        WorkItemImportMerger.Merge(
            stored,
            Imported(title: "Contoso renewal blocked by billing"),
            WorkItemOrigin.AzureDevOps,
            (ContentSensitivityPolicy)99,
            isNewItem: true,
            SyncedAt);

        Assert.Equal("AB#4182", stored.Title);
        Assert.Equal(string.Empty, stored.Description);
        Assert.Null(stored.ExternalUrl);
    }

    [Fact]
    public void Merge_TruncatesATitleThatWouldNotFitTheColumn()
    {
        // A single oversized title must not fail the whole sync on a database constraint,
        // which would leave the squad with no imported data at all and a stack trace to read.
        var stored = new WorkItem();

        WorkItemImportMerger.Merge(
            stored,
            Imported(title: string.Join(" ", Enumerable.Repeat("feature", 70))),
            WorkItemOrigin.AzureDevOps,
            ContentSensitivityPolicy.RedactedTitles,
            isNewItem: true,
            SyncedAt);

        Assert.Equal(300, stored.Title.Length);
    }

    [Fact]
    public void Merge_FromALiveSync_ClearsAStageTimestampTheBoardNoLongerReports()
    {
        // A live sync reads the full revision history, so a null is an observation: the card
        // was moved back and never genuinely reached that stage. Keeping the old value would
        // report a review latency for a review that has not happened.
        var stored = new WorkItem
        {
            Origin = WorkItemOrigin.AzureDevOps,
            PrCreatedAtUtc = new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc)
        };

        WorkItemImportMerger.Merge(
            stored,
            Imported(timeline: new StageTimeline { PickedUpAtUtc = BoardCreatedAt }),
            WorkItemOrigin.AzureDevOps,
            ContentSensitivityPolicy.MetricsOnly,
            isNewItem: false,
            SyncedAt);

        Assert.Null(stored.PrCreatedAtUtc);
        Assert.Equal(BoardCreatedAt, stored.PickedUpAtUtc);
    }

    [Fact]
    public void Merge_FromAFileImport_NeverErasesAStageTimestampAnEarlierSyncEstablished()
    {
        // A CSV holds current column values and no history, so almost every stage comes back
        // null. Honouring those nulls would wipe measured timestamps and silently reset every
        // cycle time built on them - and a file import is the one route a user runs casually.
        var measured = new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc);

        var stored = new WorkItem
        {
            Origin = WorkItemOrigin.AzureDevOps,
            PrCreatedAtUtc = measured,
            PrApprovedAtUtc = measured.AddHours(6)
        };

        WorkItemImportMerger.Merge(
            stored,
            Imported(timeline: new StageTimeline()),
            WorkItemOrigin.FileImport,
            ContentSensitivityPolicy.MetricsOnly,
            isNewItem: false,
            SyncedAt);

        Assert.Equal(measured, stored.PrCreatedAtUtc);
        Assert.Equal(measured.AddHours(6), stored.PrApprovedAtUtc);
    }

    [Fact]
    public void Merge_FromAFileImport_StillFillsAStageTimestampThatWasMissing()
    {
        // The other half of the same rule: an export may add what it knows. "Never erase" must
        // not degrade into "never write", or a file import would stop being useful at all.
        var stored = new WorkItem { Origin = WorkItemOrigin.FileImport };
        var closedAt = new DateTime(2026, 9, 28, 16, 45, 0, DateTimeKind.Utc);

        WorkItemImportMerger.Merge(
            stored,
            Imported(timeline: new StageTimeline { CompletedAtUtc = closedAt }),
            WorkItemOrigin.FileImport,
            ContentSensitivityPolicy.MetricsOnly,
            isNewItem: false,
            SyncedAt);

        Assert.Equal(closedAt, stored.CompletedAtUtc);
    }

    [Fact]
    public void Merge_TakesTheCreationDateFromTheBoard()
    {
        // Pickup latency is measured from creation. Using the row's own insertion time would
        // report how long ago the sync ran rather than how long the item waited.
        var stored = new WorkItem { CreatedAtUtc = SyncedAt };

        WorkItemImportMerger.Merge(
            stored,
            Imported(createdAtUtc: BoardCreatedAt),
            WorkItemOrigin.AzureDevOps,
            ContentSensitivityPolicy.MetricsOnly,
            isNewItem: true,
            SyncedAt);

        Assert.Equal(BoardCreatedAt, stored.CreatedAtUtc);
    }

    [Fact]
    public void Merge_KeepsTheStoredCreationDate_WhenTheImportCarriesNone()
    {
        // Writing default(DateTime) would date every such item to year one and produce a
        // pickup latency measured in centuries.
        var existing = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        var stored = new WorkItem { Origin = WorkItemOrigin.AzureDevOps, CreatedAtUtc = existing };

        WorkItemImportMerger.Merge(
            stored,
            Imported(createdAtUtc: default),
            WorkItemOrigin.AzureDevOps,
            ContentSensitivityPolicy.MetricsOnly,
            isNewItem: false,
            SyncedAt);

        Assert.Equal(existing, stored.CreatedAtUtc);
    }
}
