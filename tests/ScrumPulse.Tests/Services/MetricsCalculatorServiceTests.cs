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

        var baseTime = new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc); // Monday 9:00 AM

        // 2 completed items = 16 points (80% Say-Do)
        // Item 1: Pickup 2.0h, Dev 10.0h, Review 4.0h, Merge 1.0h, QA 3.0h
        var item1 = new WorkItem
        {
            Id = Guid.NewGuid(),
            SprintId = sprintId,
            Key = "SP-101",
            Title = "Feature Alpha",
            Status = WorkItemStatus.Done,
            StoryPoints = 8,
            CreatedAtUtc = baseTime,                                          // Monday 09:00
            PickedUpAtUtc = new DateTime(2026, 8, 3, 11, 0, 0, DateTimeKind.Utc), // Monday 11:00 (2.0h)
            PrCreatedAtUtc = new DateTime(2026, 8, 4, 12, 30, 0, DateTimeKind.Utc), // Mon 11:00-17:30 (6.5h) + Tue 09:00-12:30 (3.5h) = 10.0h
            PrApprovedAtUtc = new DateTime(2026, 8, 4, 16, 30, 0, DateTimeKind.Utc), // Tue 12:30-16:30 = 4.0h
            PrMergedAtUtc = new DateTime(2026, 8, 4, 17, 30, 0, DateTimeKind.Utc), // Tue 16:30-17:30 = 1.0h
            QaStartedAtUtc = new DateTime(2026, 8, 4, 17, 30, 0, DateTimeKind.Utc),
            CompletedAtUtc = new DateTime(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc)  // Wed 09:00-12:00 = 3.0h (overnight Tue 17:30-Wed 09:00 excluded)
        };
        // Item 2: Pickup 3.0h, Dev 12.0h, Review 5.0h, Merge 1.0h, QA 4.0h
        var item2 = new WorkItem
        {
            Id = Guid.NewGuid(),
            SprintId = sprintId,
            Key = "SP-102",
            Title = "Feature Beta",
            Status = WorkItemStatus.Done,
            StoryPoints = 8,
            CreatedAtUtc = baseTime,                                          // Monday 09:00
            PickedUpAtUtc = new DateTime(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc), // Monday 12:00 (3.0h)
            PrCreatedAtUtc = new DateTime(2026, 8, 4, 15, 30, 0, DateTimeKind.Utc), // Mon 12:00-17:30 (5.5h) + Tue 09:00-15:30 (6.5h) = 12.0h
            PrApprovedAtUtc = new DateTime(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc), // Tue 15:30-17:30 (2.0h) + Wed 09:00-12:00 (3.0h) = 5.0h
            PrMergedAtUtc = new DateTime(2026, 8, 5, 13, 0, 0, DateTimeKind.Utc), // Wed 12:00-13:00 = 1.0h
            QaStartedAtUtc = new DateTime(2026, 8, 5, 13, 0, 0, DateTimeKind.Utc),
            CompletedAtUtc = new DateTime(2026, 8, 5, 17, 0, 0, DateTimeKind.Utc)  // Wed 13:00-17:00 = 4.0h
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

    [Fact]
    public void CalculateWorkingDays_August31ToSeptember11_ReturnsExact10WorkingDays()
    {
        // Arrange
        var start = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc); // Monday
        var end = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc);   // Friday

        // Act
        int workingDays = MetricsCalculatorService.CalculateWorkingDays(start, end);

        // Assert: Mon Aug 31 - Fri Sep 4 (5) + Mon Sep 7 - Fri Sep 11 (5) = exactly 10 days
        Assert.Equal(10, workingDays);
    }

    [Fact]
    public async Task CalculateSprintCapacityAsync_With8Point5DailyHours_Computes85HoursPerDeveloper()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var sprintId = Guid.NewGuid();
        var sprint = new Sprint
        {
            Id = sprintId,
            Name = "Sprint 2",
            StartDate = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc),
            DailyWorkingHours = 8.5,
            CommittedStoryPoints = 30
        };
        db.Sprints.Add(sprint);

        var member = new TeamMember
        {
            Id = Guid.NewGuid(),
            Name = "Kaushik",
            Role = RoleType.Developer,
            IsActive = true
        };
        db.TeamMembers.Add(member);
        await db.SaveChangesAsync();

        var service = new MetricsCalculatorService(db);

        // Act
        var capacity = await service.CalculateSprintCapacityAsync(sprintId);

        // Assert: 10 working days * 8.5 hours = 85.0 hours
        Assert.NotNull(capacity);
        Assert.Single(capacity.MemberBreakdown);
        Assert.Equal(10, capacity.MemberBreakdown[0].WorkingDays);
        Assert.Equal(85.0, capacity.MemberBreakdown[0].AvailableHours);
        Assert.Equal(85.0, capacity.TotalAvailableHours);
    }

    [Fact]
    public async Task GetVelocityTrendAsync_CalculatesChronologicalTrendAndRollingAverage()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var s1Id = Guid.NewGuid();
        var s2Id = Guid.NewGuid();
        var s1 = new Sprint { Id = s1Id, Name = "Sprint 1", StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = new DateTime(2026, 7, 14, 0, 0, 0, DateTimeKind.Utc), CommittedStoryPoints = 20 };
        var s2 = new Sprint { Id = s2Id, Name = "Sprint 2", StartDate = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc), EndDate = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc), CommittedStoryPoints = 30 };
        db.Sprints.AddRange(s1, s2);

        var item1 = new WorkItem { Id = Guid.NewGuid(), SprintId = s1Id, Status = WorkItemStatus.Done, StoryPoints = 18, Title = "PBI 1" };
        var item2 = new WorkItem { Id = Guid.NewGuid(), SprintId = s2Id, Status = WorkItemStatus.Done, StoryPoints = 27, Title = "PBI 2" };
        db.WorkItems.AddRange(item1, item2);
        await db.SaveChangesAsync();

        var service = new MetricsCalculatorService(db);

        // Act
        var trend = await service.GetVelocityTrendAsync(6);

        // Assert
        Assert.NotNull(trend);
        Assert.Equal(2, trend.Sprints.Count);
        Assert.Equal(18, trend.Sprints[0].DeliveredPoints);
        Assert.Equal(27, trend.Sprints[1].DeliveredPoints);
        Assert.Equal(18.0, trend.Sprints[0].RollingAverageVelocity);
        Assert.Equal(22.5, trend.Sprints[1].RollingAverageVelocity); // (18 + 27) / 2 = 22.5
        Assert.Equal(22.5, trend.AverageVelocity);
    }

    [Fact]
    public async Task CalculateSprintHealthAsync_ComputesCompositeScoreAndFactors()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var sprintId = Guid.NewGuid();
        var sprint = new Sprint { Id = sprintId, Name = "Sprint 10", CommittedStoryPoints = 20 };
        db.Sprints.Add(sprint);

        var item = new WorkItem
        {
            Id = Guid.NewGuid(),
            SprintId = sprintId,
            Status = WorkItemStatus.Done,
            StoryPoints = 20,
            Title = "Story Done",
            PrCreatedAtUtc = DateTime.UtcNow.AddHours(-3.5),
            PrApprovedAtUtc = DateTime.UtcNow,
            IsEscapedDefect = false
        };
        db.WorkItems.Add(item);

        var standup = new DailyStandup
        {
            Id = Guid.NewGuid(),
            SprintId = sprintId,
            TeamMemberId = Guid.NewGuid(),
            MoodScore = 5,
            StandupDate = DateTime.UtcNow,
            YesterdaySummary = "Done",
            TodayPlan = "Next"
        };
        db.DailyStandups.Add(standup);
        await db.SaveChangesAsync();

        var service = new MetricsCalculatorService(db);

        // Act
        var health = await service.CalculateSprintHealthAsync(sprintId);

        // Assert
        Assert.NotNull(health);
        Assert.Equal(sprintId, health.SprintId);
        Assert.True(health.OverallScore >= 80, $"Expected high score for clean sprint, got {health.OverallScore}");
        Assert.Equal("Optimal", health.HealthGrade);
        Assert.Equal(6, health.Factors.Count);
    }

    [Fact]
    public async Task CompareSprintsAsync_CalculatesDeltasAndSummary()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var sprint1 = new Sprint
        {
            Id = Guid.NewGuid(),
            Name = "Sprint 10",
            CommittedStoryPoints = 30,
            StartDate = DateTime.UtcNow.AddDays(-28),
            EndDate = DateTime.UtcNow.AddDays(-14)
        };
        var sprint2 = new Sprint
        {
            Id = Guid.NewGuid(),
            Name = "Sprint 11",
            CommittedStoryPoints = 35,
            StartDate = DateTime.UtcNow.AddDays(-14),
            EndDate = DateTime.UtcNow
        };
        db.Sprints.AddRange(sprint1, sprint2);

        // Sprint 1: delivered 20 pts (1 story), 2 blockers
        db.WorkItems.Add(new WorkItem
        {
            Id = Guid.NewGuid(),
            SprintId = sprint1.Id,
            Status = WorkItemStatus.Done,
            StoryPoints = 20,
            Title = "Sprint 1 Story"
        });
        db.Blockers.AddRange(
            new Blocker { Id = Guid.NewGuid(), SprintId = sprint1.Id, Title = "B1" },
            new Blocker { Id = Guid.NewGuid(), SprintId = sprint1.Id, Title = "B2" }
        );

        // Sprint 2: delivered 32 pts (1 story), 0 blockers
        db.WorkItems.Add(new WorkItem
        {
            Id = Guid.NewGuid(),
            SprintId = sprint2.Id,
            Status = WorkItemStatus.Done,
            StoryPoints = 32,
            Title = "Sprint 2 Story"
        });

        await db.SaveChangesAsync();

        var service = new MetricsCalculatorService(db);

        // Act
        var comparison = await service.CompareSprintsAsync(sprint1.Id, sprint2.Id);

        // Assert
        Assert.NotNull(comparison);
        Assert.Equal(sprint1.Id, comparison.SprintAId);
        Assert.Equal(sprint2.Id, comparison.SprintBId);
        Assert.Equal("Sprint 10", comparison.SprintAName);
        Assert.Equal("Sprint 11", comparison.SprintBName);

        var velocityMetric = comparison.Metrics.First(m => m.MetricName == "Delivered Story Points");
        Assert.Equal(20, velocityMetric.ValueSprintA);
        Assert.Equal(32, velocityMetric.ValueSprintB);
        Assert.Equal(12, velocityMetric.Delta);
        Assert.True(velocityMetric.IsImprovement);

        var blockerMetric = comparison.Metrics.First(m => m.MetricName == "Total Blockers Encountered");
        Assert.Equal(2, blockerMetric.ValueSprintA);
        Assert.Equal(0, blockerMetric.ValueSprintB);
        Assert.Equal(-2, blockerMetric.Delta);
        Assert.True(blockerMetric.IsImprovement);
    }

    [Fact]
    public async Task CalculateSprintCapacityAsync_DeductsBlockerHoursAndCalibratesCapacity()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var sprintId = Guid.NewGuid();
        var sprint = new Sprint
        {
            Id = sprintId,
            Name = "Sprint Capacity Impediment Test",
            StartDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc),
            DailyWorkingHours = 8.0,
            CommittedStoryPoints = 40
        };
        db.Sprints.Add(sprint);

        var memberId = Guid.NewGuid();
        var member = new TeamMember
        {
            Id = memberId,
            Name = "Jane Engineer",
            Role = RoleType.Developer,
            IsActive = true
        };
        db.TeamMembers.Add(member);

        // Blocker with 16 blocked hours (2 full working days lost)
        var blocker = new Blocker
        {
            Id = Guid.NewGuid(),
            SprintId = sprintId,
            RaisedById = memberId,
            Title = "Staging Cluster Down",
            Description = "Kubernetes nodes crashing",
            BlockedHours = 16.0,
            SlaHoursLimit = 8
        };
        db.Blockers.Add(blocker);
        await db.SaveChangesAsync();

        var service = new MetricsCalculatorService(db);

        // Act
        var capacity = await service.CalculateSprintCapacityAsync(sprintId);

        // Assert
        Assert.NotNull(capacity);
        Assert.Equal(16.0, capacity.TotalBlockerHours);
        Assert.True(capacity.NetAvailableHours < capacity.TotalAvailableHours);
        Assert.Equal(Math.Round(capacity.TotalAvailableHours - 16.0, 1), capacity.NetAvailableHours);

        // Member breakdown reflects blocker hours
        var memberCap = Assert.Single(capacity.MemberBreakdown);
        Assert.Equal(16.0, memberCap.BlockerHours);
        Assert.Equal(Math.Round(memberCap.AvailableHours - 16.0, 1), memberCap.NetAvailableHours);
    }

    [Fact]
    public async Task GenerateExecutiveReportAsync_CalculatesBlockerCapacityDragAndImpactStatement()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var sprintId = Guid.NewGuid();
        var sprint = new Sprint
        {
            Id = sprintId,
            Name = "Sprint Executive Blocker Test",
            Goal = "Velocity stability",
            CommittedStoryPoints = 30
        };
        db.Sprints.Add(sprint);

        var blocker1 = new Blocker
        {
            Id = Guid.NewGuid(),
            SprintId = sprintId,
            Title = "API Spec Delay",
            Description = "Waiting on 3rd party",
            BlockedHours = 12.0
        };
        var blocker2 = new Blocker
        {
            Id = Guid.NewGuid(),
            SprintId = sprintId,
            Title = "Database Migration Lock",
            Description = "Deadlock on staging",
            BlockedHours = 4.0
        };
        db.Blockers.AddRange(blocker1, blocker2);
        await db.SaveChangesAsync();

        var service = new MetricsCalculatorService(db);

        // Act
        var report = await service.GenerateExecutiveReportAsync(sprintId);

        // Assert
        Assert.NotNull(report);
        Assert.Equal(16.0, report.TotalBlockedHours);
        Assert.Equal(2, report.LostStoryPointsCapacity); // 16h / 8h per story point = 2 pts
        Assert.Contains("Because of blockers for 16 hours, our capacity went down by 16h", report.BlockerCapacityImpactSummary);
        Assert.Contains("Because of blockers for 16 hours, our capacity went down by 16h", report.ExecutiveSummaryMarkdown);
    }

    [Fact]
    public async Task GenerateExecutiveReportAsync_ZeroBlockers_HasZeroDragAndNoSlowdownNarrative()
    {
        using var db = CreateInMemoryDbContext();
        var sprintId = Guid.NewGuid();
        var sprint = new Sprint
        {
            Id = sprintId,
            Name = "Sprint Zero Blocker Test",
            CommittedStoryPoints = 20
        };
        db.Sprints.Add(sprint);
        await db.SaveChangesAsync();

        var service = new MetricsCalculatorService(db);
        var report = await service.GenerateExecutiveReportAsync(sprintId);

        Assert.NotNull(report);
        Assert.Equal(0, report.TotalBlockedHours);
        Assert.Equal(0, report.LostStoryPointsCapacity);
        Assert.Contains("No blocker hours recorded", report.BlockerCapacityImpactSummary);
    }

    [Fact]
    public async Task CalculateSprintCapacityAsync_BlockerHoursExceedGross_ClampsNetCapacityToZero()
    {
        using var db = CreateInMemoryDbContext();
        var sprintId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var member = new TeamMember
        {
            Id = memberId,
            Name = "Dev Single",
            IsActive = true
        };
        db.TeamMembers.Add(member);

        var sprint = new Sprint
        {
            Id = sprintId,
            Name = "Short Sprint",
            StartDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc), // 2 days * 8h = 16h
            DailyWorkingHours = 8.0,
            IsActive = true
        };
        db.Sprints.Add(sprint);

        // Blocker with 50 hours (exceeding 16h gross)
        var blocker = new Blocker
        {
            Id = Guid.NewGuid(),
            SprintId = sprintId,
            Title = "Major Fire",
            Description = "Total lockdown",
            BlockedHours = 50.0
        };
        db.Blockers.Add(blocker);
        await db.SaveChangesAsync();

        var service = new MetricsCalculatorService(db);
        var capacity = await service.CalculateSprintCapacityAsync(sprintId);

        Assert.NotNull(capacity);
        Assert.Equal(16.0, capacity.TotalAvailableHours);
        Assert.Equal(50.0, capacity.TotalBlockerHours);
        Assert.Equal(0.0, capacity.NetAvailableHours); // Clamped via Math.Max(0, ...)
    }
}
