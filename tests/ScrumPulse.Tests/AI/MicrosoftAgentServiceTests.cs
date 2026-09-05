namespace ScrumPulse.Tests.AI;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ScrumPulse.AI.Configuration;
using ScrumPulse.AI.Services;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Infrastructure.Persistence;
using ScrumPulse.Infrastructure.Services;
using Xunit;

public class MicrosoftAgentServiceTests
{
    private (AppDbContext db, MicrosoftAgentService aiService) CreateTestService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"ScrumPulse_AiTestDb_{Guid.NewGuid()}")
            .Options;

        var db = new AppDbContext(options);
        var store = new MemoryIdempotencyStore();
        var config = new AgentConfiguration();
        var aiService = new MicrosoftAgentService(db, store, config, NullLogger<MicrosoftAgentService>.Instance);
        return (db, aiService);
    }

    [Fact]
    public async Task AnalyzeIndividualPerformanceAsync_ReturnsSynthesizedRecommendations()
    {
        var (db, aiService) = CreateTestService();
        using (db)
        {
            var memberId = Guid.NewGuid();
            var result = await aiService.GenerateIndividualCoachingAsync(memberId);

            Assert.NotNull(result);
            Assert.Contains("Coaching Plan", result.Title);
            Assert.NotEmpty(result.Summary);
            Assert.NotEmpty(result.KeyFindings);
            Assert.NotEmpty(result.ActionableRecommendations);
            Assert.NotEmpty(result.RiskLevel);
        }
    }

    [Fact]
    public async Task AnalyzeProjectRisksAsync_ReturnsRiskRadarInsights()
    {
        var (db, aiService) = CreateTestService();
        using (db)
        {
            var sprintId = Guid.NewGuid();
            var result = await aiService.GenerateProjectSprintInsightsAsync(sprintId);

            Assert.NotNull(result);
            Assert.Contains("Sprint Risk", result.Title);
            Assert.NotEmpty(result.Summary);
            Assert.NotEmpty(result.KeyFindings);
            Assert.NotEmpty(result.ActionableRecommendations);
        }
    }

    [Fact]
    public async Task AnalyzeCompanyStrategicCollaborationAsync_WhenEmpty_ReturnsNoDataToAnalyze()
    {
        var (db, aiService) = CreateTestService();
        using (db)
        {
            var result = await aiService.GenerateCompanyStrategicInsightsAsync();

            Assert.NotNull(result);
            Assert.Contains("Strategic", result.Title);
            Assert.Equal("No Data (Telemetry Pending)", result.RiskLevel);
            Assert.Contains(result.KeyFindings, f => f.Contains("NO DATA TO ANALYZE"));
            Assert.NotEmpty(result.ActionableRecommendations);
        }
    }

    [Fact]
    public async Task AnalyzeCompanyStrategicCollaborationAsync_WithData_ComputesFactualMetrics()
    {
        var (db, aiService) = CreateTestService();
        using (db)
        {
            var sprint = new ScrumPulse.Domain.Entities.Sprint
            {
                Id = Guid.NewGuid(),
                Name = "Sprint 1",
                StartDate = DateTime.UtcNow.AddDays(-14),
                EndDate = DateTime.UtcNow.AddDays(-1),
                IsActive = false
            };
            db.Sprints.Add(sprint);

            var item = new ScrumPulse.Domain.Entities.WorkItem
            {
                Id = Guid.NewGuid(),
                SprintId = sprint.Id,
                Title = "Feature A",
                StoryPoints = 8,
                Status = ScrumPulse.Domain.Enums.WorkItemStatus.Done
            };
            db.WorkItems.Add(item);
            await db.SaveChangesAsync();

            var result = await aiService.GenerateCompanyStrategicInsightsAsync();

            Assert.NotNull(result);
            Assert.DoesNotContain("No Data", result.RiskLevel);
            Assert.Contains(result.KeyFindings, f => f.Contains("8 story points"));
            Assert.DoesNotContain(result.KeyFindings, f => f.Contains("18% over the past 3 sprints"));
        }
    }

    [Theory]
    [InlineData("Developer", "How do I improve PR turnaround?")]
    [InlineData("ScrumMaster", "How can we unblock client dependencies?")]
    [InlineData("QaEngineer", "What are the escaped defects in staging?")]
    [InlineData("Cdl", "How to conduct monthly 1:1 reviews?")]
    [InlineData("ClientStakeholder", "What is our offshore delivery predictability?")]
    public async Task AskAgileCopilotAsync_ProvidesTailoredContextualAnswersForDifferentRoles(string role, string prompt)
    {
        var (db, aiService) = CreateTestService();
        using (db)
        {
            var response = await aiService.ProcessCopilotChatAsync(new CopilotChatRequest(prompt, role));

            Assert.NotNull(response);
            Assert.NotEmpty(response.Answer);
            Assert.NotEmpty(response.SuggestedFollowUps);
        }
    }
}
