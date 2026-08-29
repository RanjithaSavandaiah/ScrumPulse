namespace ScrumPulse.Tests.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Api.Controllers;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Infrastructure.Persistence;
using Xunit;

public class PullRequestsControllerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"ScrumPulse_PrDb_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Create_And_GetDeveloperMetrics_CalculatesActionabilityCorrectly()
    {
        using var db = CreateInMemoryDb();
        var dev = new TeamMember { Name = "Kaushik (Developer)", Email = "kaushik@test.com", Role = RoleType.Developer };
        var reviewer = new TeamMember { Name = "Athul (Developer)", Email = "athul@test.com", Role = RoleType.Developer };
        var sprint = new Sprint { Name = "Sprint 25", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(14) };

        db.TeamMembers.AddRange(dev, reviewer);
        db.Sprints.Add(sprint);
        await db.SaveChangesAsync();

        var controller = new PullRequestsController(db);

        // Create PR 1: 10 total comments, 4 actionable
        var request1 = new CreatePullRequestLogRequest(
            WorkItemId: null,
            AuthorId: dev.Id,
            ReviewerId: reviewer.Id,
            SprintId: sprint.Id,
            PrNumber: "PR-101",
            PrTitle: "OAuth 2.0 PKCE flow",
            PrUrl: "https://github.com/org/repo/pull/101",
            TotalCommentsCount: 10,
            ActionableCommentsCount: 4,
            ReviewSummary: "Refactored token refresh logic",
            ReviewStatus: "Approved"
        );

        // Create PR 2: 6 total comments, 2 actionable
        var request2 = new CreatePullRequestLogRequest(
            WorkItemId: null,
            AuthorId: dev.Id,
            ReviewerId: reviewer.Id,
            SprintId: sprint.Id,
            PrNumber: "PR-102",
            PrTitle: "Staging Webhook integration",
            PrUrl: "https://github.com/org/repo/pull/102",
            TotalCommentsCount: 6,
            ActionableCommentsCount: 2,
            ReviewSummary: "Added retry backoff for webhooks",
            ReviewStatus: "Merged"
        );

        await controller.Create(request1, Ct);
        await controller.Create(request2, Ct);

        // Fetch Developer Metrics
        var result = await controller.GetDeveloperMetrics(sprint.Id, Ct);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var metrics = Assert.IsAssignableFrom<IEnumerable<DeveloperPrMetricsDto>>(okResult.Value);

        var devMetric = metrics.First(m => m.DeveloperId == dev.Id);
        Assert.Equal(2, devMetric.TotalPrsCreated);
        Assert.Equal(16, devMetric.TotalCommentsReceived);
        Assert.Equal(6, devMetric.ActionableCommentsReceived);
        Assert.Equal(37.5, devMetric.ActionabilityRatePercentage); // (6 / 16) * 100
        Assert.Equal(8.0, devMetric.AvgCommentsPerPr); // 16 / 2
        Assert.Equal(2, devMetric.Prs.Count);
    }
}
