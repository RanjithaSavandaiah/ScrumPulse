namespace ScrumPulse.Tests.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Infrastructure.Persistence;
using ScrumPulse.Infrastructure.Services;
using Xunit;

public class TeamPerformanceServiceTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"ScrumPulse_PerfTestDb_{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetPerformanceSummaryAsync_WhenNoCompletedSprints_ReturnsGracefulEmptyState()
    {
        using var db = CreateInMemoryDb();
        var service = new TeamPerformanceService(db, NullLogger<TeamPerformanceService>.Instance);

        var summary = await service.GetPerformanceSummaryAsync(6);

        Assert.NotNull(summary);
        Assert.Equal(0, summary.SprintsAnalyzed);
        Assert.Equal("N/A", summary.PerformanceGrade);
        Assert.Equal(0, summary.OverallScore);
        Assert.Empty(summary.Metrics);
        Assert.Empty(summary.Highlights);
        Assert.Empty(summary.SprintSnapshots);
        Assert.Equal("No Data", summary.Engagement.EngagementGrade);
        Assert.Contains("No completed sprint telemetry", summary.Headline);
    }

    [Fact]
    public async Task GetPerformanceSummaryAsync_WithCompletedSprints_ComputesMetricsAndGradeAccurately()
    {
        using var db = CreateInMemoryDb();

        var team = new Team { Id = Guid.NewGuid(), Name = "Core Engine Squad", IsActive = true };
        db.Teams.Add(team);

        var sprint1 = new Sprint
        {
            Id = Guid.NewGuid(),
            Name = "Sprint 1",
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(-16),
            CommittedStoryPoints = 30,
            IsActive = false
        };

        var sprint2 = new Sprint
        {
            Id = Guid.NewGuid(),
            Name = "Sprint 2",
            StartDate = DateTime.UtcNow.AddDays(-15),
            EndDate = DateTime.UtcNow.AddDays(-1),
            CommittedStoryPoints = 35,
            IsActive = false
        };

        db.Sprints.AddRange(sprint1, sprint2);

        // Sprint 1 Work Items: 28 delivered
        var wi1 = new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = "Story 1.1",
            SprintId = sprint1.Id,
            Status = WorkItemStatus.Done,
            StoryPoints = 28,
            CompletedAtUtc = DateTime.UtcNow.AddDays(-17)
        };

        // Sprint 2 Work Items: 34 delivered
        var wi2 = new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = "Story 2.1",
            SprintId = sprint2.Id,
            Status = WorkItemStatus.Done,
            StoryPoints = 34,
            CompletedAtUtc = DateTime.UtcNow.AddDays(-2)
        };

        db.WorkItems.AddRange(wi1, wi2);

        // Add daily standup and kudos to populate engagement
        var member = new TeamMember { Id = Guid.NewGuid(), Name = "Alex Engineer", IsActive = true, TeamId = team.Id };
        db.TeamMembers.Add(member);

        var standup = new DailyStandup
        {
            Id = Guid.NewGuid(),
            TeamMemberId = member.Id,
            StandupDate = DateTime.UtcNow.AddDays(-5),
            MoodScore = 5,
            YesterdaySummary = "Completed auth tests",
            TodayPlan = "Starting sprint telemetry"
        };
        db.DailyStandups.Add(standup);

        var kudos = new KudosCard
        {
            Id = Guid.NewGuid(),
            SenderId = member.Id,
            ReceiverId = member.Id,
            Badge = BadgeType.InnovationStar,
            Message = "Great job on CI/CD pipeline",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-3)
        };
        db.KudosCards.Add(kudos);

        var techTalk = new TechTalkLog
        {
            Id = Guid.NewGuid(),
            Topic = "Angular 18 Signals & Performance",
            PresenterId = member.Id,
            TalkDate = DateTime.UtcNow.AddDays(-4),
            DurationMinutes = 45
        };
        db.TechTalkLogs.Add(techTalk);

        await db.SaveChangesAsync();

        var service = new TeamPerformanceService(db, NullLogger<TeamPerformanceService>.Instance);
        var summary = await service.GetPerformanceSummaryAsync(6);

        Assert.NotNull(summary);
        Assert.Equal("Core Engine Squad", summary.TeamName);
        Assert.Equal(2, summary.SprintsAnalyzed);
        Assert.NotEqual("N/A", summary.PerformanceGrade);
        Assert.True(summary.OverallScore > 0, "Overall score should be positive");
        Assert.NotEmpty(summary.Metrics);
        Assert.NotEmpty(summary.SprintSnapshots);
        Assert.NotEmpty(summary.Highlights);

        // Verify snapshot values
        var firstSnap = summary.SprintSnapshots.First(s => s.SprintId == sprint1.Id);
        Assert.Equal(30, firstSnap.CommittedPoints);
        Assert.Equal(28, firstSnap.DeliveredPoints);

        var secondSnap = summary.SprintSnapshots.First(s => s.SprintId == sprint2.Id);
        Assert.Equal(35, secondSnap.CommittedPoints);
        Assert.Equal(34, secondSnap.DeliveredPoints);

        // Engagement verification
        Assert.True(summary.Engagement.AvgMoodScore > 0);
        Assert.Equal(1, summary.Engagement.TotalKudosGiven);
        Assert.Equal(1, summary.Engagement.TechTalksDelivered);
    }

    [Fact]
    public async Task GetGrowthTrendAsync_ReturnsOrderedSnapshots()
    {
        using var db = CreateInMemoryDb();

        var sprintOld = new Sprint
        {
            Id = Guid.NewGuid(),
            Name = "Sprint Alpha",
            StartDate = DateTime.UtcNow.AddDays(-40),
            EndDate = DateTime.UtcNow.AddDays(-26),
            CommittedStoryPoints = 20,
            IsActive = false
        };

        var sprintRecent = new Sprint
        {
            Id = Guid.NewGuid(),
            Name = "Sprint Beta",
            StartDate = DateTime.UtcNow.AddDays(-20),
            EndDate = DateTime.UtcNow.AddDays(-6),
            CommittedStoryPoints = 25,
            IsActive = false
        };

        db.Sprints.AddRange(sprintOld, sprintRecent);
        await db.SaveChangesAsync();

        var service = new TeamPerformanceService(db, NullLogger<TeamPerformanceService>.Instance);
        var trend = await service.GetGrowthTrendAsync(6);

        Assert.Equal(2, trend.Count);
        Assert.Equal(sprintOld.Id, trend[0].SprintId);
        Assert.Equal(sprintRecent.Id, trend[1].SprintId);
    }
}
