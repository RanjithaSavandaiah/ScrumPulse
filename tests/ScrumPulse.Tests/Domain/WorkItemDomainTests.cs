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
        var baseTime = new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc); // Monday 9:00 AM
        var item = new WorkItem
        {
            CreatedAtUtc = baseTime,
            PickedUpAtUtc = baseTime.AddHours(2.0),       // 11:00 AM
            PrCreatedAtUtc = baseTime.AddHours(5.5),      // 14:30 PM
            PrApprovedAtUtc = baseTime.AddHours(6.5),     // 15:30 PM
            PrMergedAtUtc = baseTime.AddHours(7.5),       // 16:30 PM
            QaStartedAtUtc = baseTime.AddHours(7.5),      // 16:30 PM
            CompletedAtUtc = baseTime.AddHours(8.5)       // 17:30 PM
        };

        Assert.Equal(2.0, item.PickupLatencyHours);       // 11:00 - 09:00 = 2.0h
        Assert.Equal(3.5, item.DevCycleTimeHours);        // 14:30 - 11:00 = 3.5h
        Assert.Equal(1.0, item.PrReviewLatencyHours);     // 15:30 - 14:30 = 1.0h
        Assert.Equal(1.0, item.PrMergeLatencyHours);      // 16:30 - 15:30 = 1.0h
        Assert.Equal(1.0, item.QaTestingLatencyHours);    // 17:30 - 16:30 = 1.0h
        Assert.Equal(6.5, item.TotalCycleTimeHours);     // 17:30 - 11:00 = 6.5h
    }

    [Fact]
    public void WorkItem_PartialLatencies_HandledGracefullyWhenTimestampsMissing()
    {
        var baseTime = new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc);
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

    [Fact]
    public void WorkingHoursCalculator_SameDay_CalculatesWithinWorkingHours()
    {
        var date = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc); // Monday

        // Normal working window
        var hours1 = ScrumPulse.Domain.Common.WorkingHoursCalculator.CalculateWorkingHours(
            date.AddHours(10), date.AddHours(14.5));
        Assert.Equal(4.5, hours1);

        // Clamped at start (starts at 8:00 AM, work begins at 9:00 AM)
        var hours2 = ScrumPulse.Domain.Common.WorkingHoursCalculator.CalculateWorkingHours(
            date.AddHours(8), date.AddHours(12));
        Assert.Equal(3.0, hours2);

        // Clamped at end (ends at 19:00 PM, work ends at 17:30 PM)
        var hours3 = ScrumPulse.Domain.Common.WorkingHoursCalculator.CalculateWorkingHours(
            date.AddHours(16), date.AddHours(19));
        Assert.Equal(1.5, hours3);

        // Fully outside working hours (evening)
        var hours4 = ScrumPulse.Domain.Common.WorkingHoursCalculator.CalculateWorkingHours(
            date.AddHours(18), date.AddHours(20));
        Assert.Equal(0.0, hours4);
    }

    [Fact]
    public void WorkingHoursCalculator_NextDay_ExcludesOvernightHours()
    {
        // Wednesday 16:00 (4:00 PM) to Thursday 11:00 AM
        var start = new DateTime(2026, 8, 5, 16, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 8, 6, 11, 0, 0, DateTimeKind.Utc);

        var hours = ScrumPulse.Domain.Common.WorkingHoursCalculator.CalculateWorkingHours(start, end);
        // Wednesday: 16:00 - 17:30 = 1.5h
        // Overnight: 17:30 - 09:00 = 0.0h (excluded!)
        // Thursday: 09:00 - 11:00 = 2.0h
        // Total: 3.5h (calendar hours would be 19.0h)
        Assert.Equal(3.5, hours);
    }

    [Fact]
    public void WorkingHoursCalculator_OverWeekend_ExcludesSaturdayAndSunday()
    {
        // Friday 16:00 to Monday 11:00 AM
        var start = new DateTime(2026, 8, 7, 16, 0, 0, DateTimeKind.Utc); // Friday
        var end = new DateTime(2026, 8, 10, 11, 0, 0, DateTimeKind.Utc);  // Monday

        var hours = ScrumPulse.Domain.Common.WorkingHoursCalculator.CalculateWorkingHours(start, end);
        // Friday: 1.5h
        // Saturday & Sunday: 0.0h (excluded!)
        // Monday: 2.0h
        // Total: 3.5h (calendar hours would be 67.0h)
        Assert.Equal(3.5, hours);
    }

    [Fact]
    public void WorkingHoursCalculator_WeekendOnly_ReturnsZero()
    {
        var start = new DateTime(2026, 8, 8, 10, 0, 0, DateTimeKind.Utc); // Saturday
        var end = new DateTime(2026, 8, 9, 14, 0, 0, DateTimeKind.Utc);   // Sunday

        var hours = ScrumPulse.Domain.Common.WorkingHoursCalculator.CalculateWorkingHours(start, end);
        Assert.Equal(0.0, hours);
    }

    [Fact]
    public void WorkingHoursCalculator_EndBeforeOrEqualStart_ReturnsZero()
    {
        var time = new DateTime(2026, 8, 3, 10, 0, 0, DateTimeKind.Utc);

        Assert.Equal(0.0, ScrumPulse.Domain.Common.WorkingHoursCalculator.CalculateWorkingHours(time, time));
        Assert.Equal(0.0, ScrumPulse.Domain.Common.WorkingHoursCalculator.CalculateWorkingHours(time, time.AddHours(-2)));
    }
}
