namespace ScrumPulse.Tests.Services;

using Microsoft.EntityFrameworkCore;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Infrastructure.Persistence;
using ScrumPulse.Infrastructure.Services;
using Xunit;

public class MetricsCalculatorServiceTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"ScrumPulse_TestDb_{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task CalculateSprintCapacityAsync_DeductsLeaveDaysAndComputesPointsCorrectly()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var sprintId = Guid.NewGuid();
        var sprint = new Sprint
        {
            Id = sprintId,
            Name = "Test Sprint 1",
            StartDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc),
            CommittedStoryPoints = 30
        };
        db.Sprints.Add(sprint);

        var memberId = Guid.NewGuid();
        var member = new TeamMember
        {
            Id = memberId,
            Name = "John Developer",
            Role = RoleType.Developer,
            IsActive = true
        };
        db.TeamMembers.Add(member);

        // 2 days approved leave
        var leave = new TeamLeave
        {
            Id = Guid.NewGuid(),
            TeamMemberId = memberId,
            StartDate = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 8, 4, 0, 0, 0, DateTimeKind.Utc),
            IsApproved = true
        };
        db.TeamLeaves.Add(leave);
        await db.SaveChangesAsync();

        var service = new MetricsCalculatorService(db);

        // Act
        var capacity = await service.CalculateSprintCapacityAsync(sprintId);

        // Assert
        Assert.NotNull(capacity);
        Assert.Equal(sprintId, capacity.SprintId);
        Assert.Equal("Test Sprint 1", capacity.SprintName);
        Assert.Equal(1, capacity.TotalTeamMembers);
        Assert.Equal(2, capacity.TotalLeaveDays);
        Assert.True(capacity.TotalAvailableHours > 0);
        Assert.True(capacity.RecommendedStoryPoints > 0);
        Assert.Single(capacity.MemberBreakdown);
        Assert.Equal(2, capacity.MemberBreakdown[0].LeaveDays);
    }

    [Fact]
    public async Task GenerateExecutiveReportAsync_CalculatesSayDoRatioAndStageLatencies()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var sprintId = Guid.NewGuid();
        var sprint = new Sprint
        {
            Id = sprintId,
            Name = "Sprint 24",
            Goal = "Deliver core features",
            CommittedStoryPoints = 20
        };
        db.Sprints.Add(sprint);

        var baseTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        // 2 completed items = 16 points (80% Say-Do)
        var item1 = new WorkItem
        {
            Id = Guid.NewGuid(),
            SprintId = sprintId,
            Key = "SP-101",
            Title = "Feature Alpha",
            Status = WorkItemStatus.Done,
            StoryPoints = 8,
            CreatedAtUtc = baseTime,
            PickedUpAtUtc = baseTime.AddHours(2.0),
            PrCreatedAtUtc = baseTime.AddHours(12.0),
            PrApprovedAtUtc = baseTime.AddHours(16.0),
            PrMergedAtUtc = baseTime.AddHours(17.0),
            QaStartedAtUtc = baseTime.AddHours(17.0),
            CompletedAtUtc = baseTime.AddHours(20.0)
        };
        var item2 = new WorkItem
        {
            Id = Guid.NewGuid(),
            SprintId = sprintId,
            Key = "SP-102",
            Title = "Feature Beta",
            Status = WorkItemStatus.Done,
            StoryPoints = 8,
            CreatedAtUtc = baseTime,
            PickedUpAtUtc = baseTime.AddHours(3.0),
            PrCreatedAtUtc = baseTime.AddHours(15.0),
            PrApprovedAtUtc = baseTime.AddHours(20.0),
            PrMergedAtUtc = baseTime.AddHours(21.0),
            QaStartedAtUtc = baseTime.AddHours(21.0),
            CompletedAtUtc = baseTime.AddHours(25.0)
        };
        // 1 in-flight item = 4 points
        var item3 = new WorkItem
        {
            Id = Guid.NewGuid(),
            SprintId = sprintId,
            Key = "SP-103",
            Title = "Feature Gamma",
            Status = WorkItemStatus.InProgress,
            StoryPoints = 4
        };

        db.WorkItems.AddRange(item1, item2, item3);

        // 1 active blocker
        var blocker = new Blocker
        {
            Id = Guid.NewGuid(),
            SprintId = sprintId,
            Title = "API Key missing",
            RaisedAtUtc = baseTime.AddHours(-6.0),
            ResolvedAtUtc = null
        };
        db.Blockers.Add(blocker);
        await db.SaveChangesAsync();

        var service = new MetricsCalculatorService(db);

        // Act
        var report = await service.GenerateExecutiveReportAsync(sprintId);

        // Assert
        Assert.NotNull(report);
        Assert.Equal(20, report.CommittedPoints);
        Assert.Equal(16, report.DeliveredPoints);
        Assert.Equal(4, report.InFlightPoints);
        Assert.Equal(80, report.SayDoRatioPercentage); // (16/20)*100 = 80%
        Assert.Equal(1, report.ActiveBlockersCount);
        Assert.Equal(2.5, report.AvgPickupLatencyHours); // (2.0 + 3.0) / 2 = 2.5
        Assert.Equal(11.0, report.AvgDevTimeHours); // (10.0 + 12.0) / 2 = 11.0
        Assert.Equal(4.5, report.AvgPrReviewHours); // (4.0 + 5.0) / 2 = 4.5
        Assert.Equal(1.0, report.AvgPrMergeHours); // (1.0 + 1.0) / 2 = 1.0
        Assert.Equal(3.5, report.AvgQaTestingHours); // (3.0 + 4.0) / 2 = 3.5
        Assert.Equal(22.5, report.AvgTotalCycleTimeHours); // 2.5 + 11.0 + 4.5 + 1.0 + 3.5 = 22.5
        Assert.Contains("# Sprint Executive Progress & Value Summary", report.ExecutiveSummaryMarkdown);
    }
}
