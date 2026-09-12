namespace ScrumPulse.Tests.AI;

using ScrumPulse.AI.Strategies;
using Xunit;

public class InsightGeneratorStrategyTests
{
    [Fact]
    public async Task IndividualInsightGenerator_WhenEmptyContext_ReturnsNoData()
    {
        var generator = new IndividualInsightGenerator();
        Assert.Equal("Individual", generator.Level);

        var ctx = new InsightContext { MemberName = "Test Developer" };
        var result = await generator.GenerateAsync(ctx);

        Assert.Equal("Individual", result.Level);
        Assert.Equal("No Data (Telemetry Pending)", result.RiskLevel);
        Assert.Contains(result.KeyFindings, f => f.Contains("NO DATA TO ANALYZE"));
    }

    [Fact]
    public async Task IndividualInsightGenerator_WithHighPrLatency_WarnsAndSuggestsGoldenWindow()
    {
        var generator = new IndividualInsightGenerator();
        var ctx = new InsightContext
        {
            MemberName = "John Doe",
            TotalAssigned = 5,
            CompletedItems = 4,
            TotalStoryPoints = 13,
            AvgDevCycleHours = 12.5,
            AvgReviewLatencyHours = 8.2, // > 6.0 SLA
            HappinessIndex = 8,
            SmRating = 9,
            StandupCount = 10
        };

        var result = await generator.GenerateAsync(ctx);

        Assert.Contains(result.KeyFindings, f => f.Contains("WARNING - PR Latency"));
        Assert.Contains(result.ActionableRecommendations, r => r.Contains("golden review window"));
        Assert.Contains("Low", result.RiskLevel);
    }

    [Fact]
    public async Task IndividualInsightGenerator_WithLowHappiness_TriggersBurnoutSentinel()
    {
        var generator = new IndividualInsightGenerator();
        var ctx = new InsightContext
        {
            MemberName = "Jane Smith",
            TotalAssigned = 3,
            CompletedItems = 3,
            TotalStoryPoints = 8,
            AvgReviewLatencyHours = 3.0,
            HappinessIndex = 5, // < 6
            SmRating = 7,
            StandupCount = 5
        };

        var result = await generator.GenerateAsync(ctx);

        Assert.Contains(result.KeyFindings, f => f.Contains("WARNING - WELLBEING RADAR"));
        Assert.Contains("Burnout Sentinel Triggered", result.RiskLevel);
    }

    [Fact]
    public async Task SprintInsightGenerator_WhenEmptyContext_ReturnsNoData()
    {
        var generator = new SprintInsightGenerator();
        Assert.Equal("Project", generator.Level);

        var ctx = new InsightContext { SprintName = "Sprint 99" };
        var result = await generator.GenerateAsync(ctx);

        Assert.Equal("Project", result.Level);
        Assert.Equal("No Data (Telemetry Pending)", result.RiskLevel);
    }

    [Fact]
    public async Task SprintInsightGenerator_WithBlockers_FlagsHighRisk()
    {
        var generator = new SprintInsightGenerator();
        var ctx = new InsightContext
        {
            SprintName = "Sprint 12",
            CommittedPoints = 40,
            DeliveredPoints = 25,
            ActiveBlockers = 3, // > 2 -> High risk
            ConfidenceScore = 6,
            AvgTeamHappiness = 7.5,
            AvgSmRating = 8.0,
            TotalTechTalks = 2
        };

        var result = await generator.GenerateAsync(ctx);

        Assert.Contains("High", result.RiskLevel);
        Assert.Contains(result.KeyFindings, f => f.Contains("3 active blocker(s)"));
        Assert.Contains(result.ActionableRecommendations, r => r.Contains("Escalate pending client blocker"));
    }

    [Fact]
    public async Task CompanyInsightGenerator_WhenEmptyContext_ReturnsNoData()
    {
        var generator = new CompanyInsightGenerator();
        Assert.Equal("Company", generator.Level);

        var ctx = new InsightContext();
        var result = await generator.GenerateAsync(ctx);

        Assert.Equal("Company", result.Level);
        Assert.Equal("No Data (Telemetry Pending)", result.RiskLevel);
    }

    [Fact]
    public async Task CompanyInsightGenerator_WithEscapedDefectsAndBlockers_ComputesRates()
    {
        var generator = new CompanyInsightGenerator();
        var ctx = new InsightContext
        {
            SprintsCount = 5,
            TotalWorkItems = 50,
            CompletedItems = 45,
            TotalStoryPoints = 120,
            EscapedDefects = 4, // 4/50 = 8% > 5% -> High
            TotalBlockers = 10,
            ActiveBlockers = 4, // >= 3 -> High
            ResolvedBlockers = 6,
            TotalKudos = 15,
            TotalTechTalks = 6
        };

        var result = await generator.GenerateAsync(ctx);

        Assert.Contains("High", result.RiskLevel);
        Assert.Contains(result.KeyFindings, f => f.Contains("120 story points"));
        Assert.Contains(result.KeyFindings, f => f.Contains("8%"));
        Assert.Contains(result.KeyFindings, f => f.Contains("60%")); // 6/10 resolved = 60%
        Assert.Contains(result.KeyFindings, f => f.Contains("15 peer kudos"));
    }
}
