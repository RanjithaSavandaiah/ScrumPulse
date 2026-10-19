namespace ScrumPulse.Tests.Integration;

using ScrumPulse.Application.Integration;
using ScrumPulse.Domain.Enums;
using Xunit;

/// <summary>
/// Covers the translation from a board's revision history into stage timestamps.
/// </summary>
/// <remarks>
/// These figures feed the cycle-time and review-latency numbers shown to delivery leads, so
/// an error here is not a cosmetic bug: it produces a confident, wrong answer to "where is
/// the time going". The cases below are the ones where a plausible implementation goes
/// quietly wrong.
/// </remarks>
public class StageTimelineBuilderTests
{
    private static readonly DateTime SprintStart = new(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc);

    private static BoardStateChange Change(string state, double hoursAfterStart) =>
        new(state, SprintStart.AddHours(hoursAfterStart));

    private static BoardStateMapping DefaultMapping() => BoardStateMapping.FromJson(null);

    [Fact]
    public void Build_MapsAStraightforwardProgression()
    {
        var timeline = StageTimelineBuilder.Build(
            [
                Change("New", 0),
                Change("Active", 2),
                Change("Code Review", 10),
                Change("Ready to Merge", 20),
                Change("Merged", 21),
                Change("Testing", 22),
                Change("Closed", 30)
            ],
            DefaultMapping());

        Assert.Equal(SprintStart.AddHours(2), timeline.PickedUpAtUtc);
        Assert.Equal(SprintStart.AddHours(10), timeline.PrCreatedAtUtc);
        Assert.Equal(SprintStart.AddHours(20), timeline.PrApprovedAtUtc);
        Assert.Equal(SprintStart.AddHours(21), timeline.PrMergedAtUtc);
        Assert.Equal(SprintStart.AddHours(22), timeline.QaStartedAtUtc);
        Assert.Equal(SprintStart.AddHours(30), timeline.CompletedAtUtc);
    }

    [Fact]
    public void Build_KeepsTheFirstEntryIntoAStage_WhenWorkIsSentBack()
    {
        // The case that decides whether the metric is honest. An item rejected at review and
        // resubmitted has genuinely been in review since the first submission. Recording the
        // later entry would report a flattering few hours instead of the real two days.
        var timeline = StageTimelineBuilder.Build(
            [
                Change("Active", 1),
                Change("Code Review", 5),
                Change("Active", 9),
                Change("Code Review", 50)
            ],
            DefaultMapping());

        Assert.Equal(SprintStart.AddHours(5), timeline.PrCreatedAtUtc);
        Assert.Equal(SprintStart.AddHours(1), timeline.PickedUpAtUtc);
    }

    [Fact]
    public void Build_LeavesSkippedStagesNull()
    {
        // Backfilling a stage the item never entered would silently shorten the cycle time
        // computed from it.
        var timeline = StageTimelineBuilder.Build(
            [Change("Active", 1), Change("Closed", 8)],
            DefaultMapping());

        Assert.Null(timeline.PrCreatedAtUtc);
        Assert.Null(timeline.PrApprovedAtUtc);
        Assert.Null(timeline.QaStartedAtUtc);
        Assert.Equal(SprintStart.AddHours(8), timeline.CompletedAtUtc);
    }

    [Fact]
    public void Build_OrdersChangesBeforeReadingThem()
    {
        // Revision APIs do not guarantee ordering, and "first entry" is meaningless without it.
        var timeline = StageTimelineBuilder.Build(
            [Change("Code Review", 10), Change("Active", 2), Change("Code Review", 40)],
            DefaultMapping());

        Assert.Equal(SprintStart.AddHours(2), timeline.PickedUpAtUtc);
        Assert.Equal(SprintStart.AddHours(10), timeline.PrCreatedAtUtc);
    }

    [Fact]
    public void Build_ReportsUnmappedStatesRatherThanGuessing()
    {
        // A custom column defaulted to Backlog would distort every figure derived from it,
        // with nothing to indicate why.
        var timeline = StageTimelineBuilder.Build(
            [Change("Active", 1), Change("Awaiting Client Sign-off", 6), Change("Closed", 20)],
            DefaultMapping());

        Assert.Contains("Awaiting Client Sign-off", timeline.UnmappedStates);
    }

    [Fact]
    public void Build_HandlesAnEmptyHistory()
    {
        var timeline = StageTimelineBuilder.Build([], DefaultMapping());

        Assert.Null(timeline.PickedUpAtUtc);
        Assert.Null(timeline.CompletedAtUtc);
        Assert.Empty(timeline.UnmappedStates);
    }

    [Fact]
    public void Build_IgnoresBlankStates()
    {
        var timeline = StageTimelineBuilder.Build(
            [new BoardStateChange(string.Empty, SprintStart), Change("Active", 3)],
            DefaultMapping());

        Assert.Equal(SprintStart.AddHours(3), timeline.PickedUpAtUtc);
        Assert.Empty(timeline.UnmappedStates);
    }

    [Fact]
    public void Build_MatchesBoardStatesCaseInsensitively()
    {
        // Process templates and manual edits vary the casing; a case-sensitive match would
        // silently drop the transition and leave the stage null.
        var timeline = StageTimelineBuilder.Build(
            [Change("active", 2), Change("CODE REVIEW", 9)],
            DefaultMapping());

        Assert.Equal(SprintStart.AddHours(2), timeline.PickedUpAtUtc);
        Assert.Equal(SprintStart.AddHours(9), timeline.PrCreatedAtUtc);
    }

    [Fact]
    public void FromJson_FallsBackToDefaults_WhenTheStoredMappingIsCorrupt()
    {
        // Corrupt configuration must not take the sync down entirely.
        var mapping = BoardStateMapping.FromJson("{ not valid json");

        Assert.Equal(WorkItemStatus.InProgress, mapping.Resolve("Active"));
    }

    [Fact]
    public void FromJson_HonoursACustomMappingOverTheDefaults()
    {
        var customMapping = BoardStateMapping.ToJson(new Dictionary<string, WorkItemStatus>
        {
            ["Peer Check"] = WorkItemStatus.PrCreated
        });

        var mapping = BoardStateMapping.FromJson(customMapping);

        Assert.Equal(WorkItemStatus.PrCreated, mapping.Resolve("Peer Check"));
    }
}
