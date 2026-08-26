namespace ScrumPulse.Tests.Domain;

using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using Xunit;

public class WorkItemDomainTests
{
    [Fact]
    public void WorkItem_Defaults_AreSetCorrectly()
    {
        var item = new WorkItem();

        Assert.Equal(WorkItemType.UserStory, item.Type);
        Assert.Equal(WorkItemStatus.Backlog, item.Status);
        Assert.Equal(PriorityLevel.Medium, item.Priority);
        Assert.Equal(3, item.StoryPoints);
        Assert.Equal("main", item.TargetBranch);
        Assert.True(item.DorAcceptanceCriteriaDefined);
        Assert.True(item.DorDependenciesIdentified);
        Assert.True(item.DorWireframeAvailable);
        Assert.False(item.DodUnitTestsPassed);
        Assert.False(item.DodPeerReviewCompleted);
        Assert.False(item.DodMergedToMaster);
        Assert.False(item.DodStagingVerified);
        Assert.False(item.IsEscapedDefect);
        Assert.Null(item.PickupLatencyHours);
        Assert.Null(item.DevCycleTimeHours);
        Assert.Null(item.PrReviewLatencyHours);
        Assert.Null(item.PrMergeLatencyHours);
        Assert.Null(item.QaTestingLatencyHours);
        Assert.Null(item.TotalCycleTimeHours);
    }

    [Fact]
    public void WorkItem_Latencies_CalculatedAccuratelyFromTimestamps()
    {
        var baseTime = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var item = new WorkItem
        {
            CreatedAtUtc = baseTime,
            PickedUpAtUtc = baseTime.AddHours(2.5),
            PrCreatedAtUtc = baseTime.AddHours(10.5),
            PrApprovedAtUtc = baseTime.AddHours(14.5),
            PrMergedAtUtc = baseTime.AddHours(15.5),
            QaStartedAtUtc = baseTime.AddHours(16.0),
            CompletedAtUtc = baseTime.AddHours(20.0)
        };

        Assert.Equal(2.5, item.PickupLatencyHours);       // 2.5h
        Assert.Equal(8.0, item.DevCycleTimeHours);        // 10.5 - 2.5 = 8.0h
        Assert.Equal(4.0, item.PrReviewLatencyHours);     // 14.5 - 10.5 = 4.0h
        Assert.Equal(1.0, item.PrMergeLatencyHours);      // 15.5 - 14.5 = 1.0h
        Assert.Equal(4.0, item.QaTestingLatencyHours);    // 20.0 - 16.0 = 4.0h
        Assert.Equal(17.5, item.TotalCycleTimeHours);     // 20.0 - 2.5 = 17.5h
    }

    [Fact]
    public void WorkItem_PartialLatencies_HandledGracefullyWhenTimestampsMissing()
    {
        var baseTime = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var item = new WorkItem
        {
            CreatedAtUtc = baseTime,
            PickedUpAtUtc = baseTime.AddHours(3.0)
            // No PR or completion timestamps yet
        };

        Assert.Equal(3.0, item.PickupLatencyHours);
        Assert.Null(item.DevCycleTimeHours);
        Assert.Null(item.PrReviewLatencyHours);
        Assert.Null(item.PrMergeLatencyHours);
        Assert.Null(item.QaTestingLatencyHours);
        Assert.Null(item.TotalCycleTimeHours);
    }
}
