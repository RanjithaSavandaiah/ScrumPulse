namespace ScrumPulse.Tests.Domain;

using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using Xunit;

public class ExtendedDomainEntitiesTests
{
    [Fact]
    public void Monthly1on1Feedback_Initialization_Sets360PropertiesAccurately()
    {
        var feedback = new Monthly1on1Feedback
        {
            Id = Guid.NewGuid(),
            TeamMemberId = Guid.NewGuid(),
            MonthYear = "2026-09",
            ScrumMasterFeedback = "Exceptional agility and sprint backlog refinement leadership.",
            CdlFeedback = "Promotion readiness demonstrated in architecture syncs.",
            ClientFeedback = "Client demo highly praised by US stakeholders.",
            SelfReflection = "Targeting 100% automated E2E coverage across microservices.",
            SmRating = 5,
            HappinessIndex = 5,
            ActionItems = "Continue mentoring junior engineers",
            NextMonthGoals = "Deliver event streaming resilience spike",
            AiSynthesizedStrengths = "High velocity and delivery reliability",
            AiGrowthRecommendations = "Broaden domain driven design patterns"
        };

        Assert.Equal("2026-09", feedback.MonthYear);
        Assert.Equal(5, feedback.SmRating);
        Assert.Equal(5, feedback.HappinessIndex);
        Assert.Contains("Exceptional agility", feedback.ScrumMasterFeedback);
        Assert.Contains("Promotion readiness", feedback.CdlFeedback);
        Assert.Contains("Client demo", feedback.ClientFeedback);
        Assert.Contains("100% automated", feedback.SelfReflection);
        Assert.NotNull(feedback.AiSynthesizedStrengths);
    }

    [Fact]
    public void PullRequestReviewLog_CalculatesActionabilityAndInitializesProperly()
    {
        var prLog = new PullRequestReviewLog
        {
            Id = Guid.NewGuid(),
            PrNumber = "#PR-912",
            PrTitle = "Feature: Distributed Multi-Tenant Middleware",
            PrUrl = "https://github.com/org/repo/pull/912",
            TotalCommentsCount = 10,
            ActionableCommentsCount = 7,
            ReviewSummary = "Implemented retry backoff and cancellation tokens.",
            ReviewStatus = ReviewStatusType.Approved
        };

        Assert.Equal("#PR-912", prLog.PrNumber);
        Assert.Equal(10, prLog.TotalCommentsCount);
        Assert.Equal(7, prLog.ActionableCommentsCount);
        Assert.Equal(ReviewStatusType.Approved, prLog.ReviewStatus);
    }

    [Fact]
    public void DailyStandup_ValidatesMoodAndEnergyFields()
    {
        var standup = new DailyStandup
        {
            Id = Guid.NewGuid(),
            TeamMemberId = Guid.NewGuid(),
            StandupDate = DateTime.UtcNow.Date,
            YesterdaySummary = "Implemented Playwright E2E suites",
            TodayPlan = "Verify CI pipeline integration",
            BlockersText = "None",
            MoodScore = 5
        };

        Assert.Equal(5, standup.MoodScore);
        Assert.Equal("None", standup.BlockersText);
        Assert.Contains("Playwright E2E", standup.YesterdaySummary);
        Assert.Contains("CI pipeline", standup.TodayPlan);
    }

    [Fact]
    public void TechDebtItem_And_TechTalkLog_SetDefaultsProperly()
    {
        var techDebt = new TechDebtItem
        {
            Id = Guid.NewGuid(),
            Title = "Refactor legacy LINQ joins into compiled queries",
            Description = "Improve latency for 6-sprint aggregate telemetry endpoint",
            Severity = TechDebtSeverity.High,
            EstimatedHours = 16,
            Status = TechDebtStatus.Identified
        };

        Assert.Equal(TechDebtSeverity.High, techDebt.Severity);
        Assert.Equal(16, techDebt.EstimatedHours);
        Assert.Equal(TechDebtStatus.Identified, techDebt.Status);

        var techTalk = new TechTalkLog
        {
            Id = Guid.NewGuid(),
            Topic = "Building Resilient Distributed Microservices with .NET 10",
            PresenterId = Guid.NewGuid(),
            TalkDate = DateTime.UtcNow,
            DurationMinutes = 60,
            SlidesUrl = "https://slides.example.com/net10-resilience",
            KeyTakeaways = "Circuit breakers, bulkhead isolation, and rate limiters."
        };

        Assert.Equal(60, techTalk.DurationMinutes);
        Assert.Contains(".NET 10", techTalk.Topic);
        Assert.NotNull(techTalk.SlidesUrl);
    }

    [Fact]
    public void RetroCard_And_ActionItem_TrackVotesAndCompletion()
    {
        var retroCard = new RetroCard
        {
            Id = Guid.NewGuid(),
            Category = RetroCategory.WentWell,
            Content = "Zero console warnings policy maintained across all PRs",
            UpvotesCount = 8,
            IsAnonymous = true
        };

        Assert.Equal(RetroCategory.WentWell, retroCard.Category);
        Assert.Equal(8, retroCard.UpvotesCount);
        Assert.True(retroCard.IsAnonymous);

        var actionItem = new RetroActionItem
        {
            Id = Guid.NewGuid(),
            Title = "Establish automated mutation testing gate",
            IsCompleted = false,
            DueDate = DateTime.UtcNow.AddDays(14)
        };

        Assert.False(actionItem.IsCompleted);
        actionItem.IsCompleted = true;
        Assert.True(actionItem.IsCompleted);
    }

    [Fact]
    public void KudosCard_ReactionEmojis_HandlesJsonProperly()
    {
        var kudos = new KudosCard
        {
            Id = Guid.NewGuid(),
            SenderId = Guid.NewGuid(),
            ReceiverId = Guid.NewGuid(),
            Badge = BadgeType.TeamPlayer,
            Message = "Outstanding pair programming and mentorship!",
            ReactionEmojisJson = "{\"❤️\": 3, \"🚀\": 5}"
        };

        Assert.Contains("❤️", kudos.ReactionEmojisJson);
        Assert.Contains("🚀", kudos.ReactionEmojisJson);
        Assert.Equal(BadgeType.TeamPlayer, kudos.Badge);
    }

    [Fact]
    public void Team_GeneratesValidJoinCodeAndDefaults()
    {
        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = "Platform Engineering Squad",
            Description = "Core infrastructure and developer productivity tooling.",
            JoinCode = "PLT9X2",
            IsActive = true
        };

        Assert.Equal("Platform Engineering Squad", team.Name);
        Assert.Equal("PLT9X2", team.JoinCode);
        Assert.True(team.IsActive);
    }

    [Fact]
    public void Team_QualityGates_InitializesChecklistsAndJsonProperly()
    {
        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = "Apollo Squad",
            Slug = "apollo-squad",
            JoinCode = "APOLLO",
            IsActive = true,
            DorChecklistJson = "[{\"id\":\"dor-sec\",\"label\":\"Security Review\",\"isRequired\":true}]",
            DodChecklistJson = "[{\"id\":\"dod-perf\",\"label\":\"Perf Baseline Met\",\"isRequired\":false}]"
        };

        Assert.NotNull(team.DorChecklistJson);
        Assert.Contains("Security Review", team.DorChecklistJson);
        Assert.Contains("dor-sec", team.DorChecklistJson);

        Assert.NotNull(team.DodChecklistJson);
        Assert.Contains("Perf Baseline Met", team.DodChecklistJson);
        Assert.Contains("dod-perf", team.DodChecklistJson);
    }

    [Fact]
    public void WorkItem_QualityGateResults_StoresCustomCheckDictionary()
    {
        var item = new WorkItem
        {
            Id = Guid.NewGuid(),
            Key = "SP-450",
            Title = "Custom gates verification",
            QualityGateResultsJson = "{\"dor-sec\":true,\"dod-perf\":false}"
        };

        Assert.NotNull(item.QualityGateResultsJson);
        Assert.Contains("\"dor-sec\":true", item.QualityGateResultsJson);
        Assert.Contains("\"dod-perf\":false", item.QualityGateResultsJson);
    }
}
