namespace ScrumPulse.Tests.Services;

using System.Text;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Infrastructure.Persistence;
using ScrumPulse.Infrastructure.Services;
using Xunit;

public class ExportServiceTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ExportSprintCsvAsync_WhenSprintNotFound_ReturnsNull()
    {
        using var db = CreateInMemoryDbContext();
        var service = new ExportService(db);

        var result = await service.ExportSprintCsvAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task ExportSprintCsvAsync_WhenSprintExists_ReturnsValidCsvFile()
    {
        using var db = CreateInMemoryDbContext();
        var member = new TeamMember { Name = "Dev One", Role = RoleType.Developer };
        var sprint = new Sprint { Name = "Sprint 10", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(14) };
        var workItem = new WorkItem
        {
            Key = "SP-101",
            Title = "Implement Export Service",
            Assignee = member,
            Sprint = sprint,
            Type = WorkItemType.UserStory,
            Status = WorkItemStatus.Done,
            Priority = PriorityLevel.High,
            StoryPoints = 5,
            CompletedAtUtc = DateTime.UtcNow
        };
        db.WorkItems.Add(workItem);
        await db.SaveChangesAsync();

        var service = new ExportService(db);
        var result = await service.ExportSprintCsvAsync(sprint.Id);

        Assert.NotNull(result);
        Assert.Equal("text/csv", result.ContentType);
        Assert.StartsWith("Sprint_10_Report_", result.FileName);
        Assert.EndsWith(".csv", result.FileName);

        var csvText = Encoding.UTF8.GetString(result.Content);
        Assert.Contains("Key,Title,Type,Status", csvText);
        Assert.Contains("SP-101", csvText);
        Assert.Contains("Implement Export Service", csvText);
        Assert.Contains("Dev One", csvText);
    }

    [Fact]
    public async Task ExportEnterpriseJsonAsync_ReturnsSerializedBundle()
    {
        using var db = CreateInMemoryDbContext();
        var member = new TeamMember { Name = "Lead Dev", Role = RoleType.ScrumMaster };
        var sprint = new Sprint { Name = "Sprint Global", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(14) };
        db.TeamMembers.Add(member);
        db.Sprints.Add(sprint);
        await db.SaveChangesAsync();

        var service = new ExportService(db);
        var result = await service.ExportEnterpriseJsonAsync();

        Assert.NotNull(result);
        Assert.Equal("application/json", result.ContentType);
        Assert.StartsWith("ScrumPulse_Export_", result.FileName);

        var json = Encoding.UTF8.GetString(result.Content);
        Assert.Contains("ScrumPulse Enterprise", json);
        Assert.Contains("Sprint Global", json);
        Assert.Contains("Lead Dev", json);
    }
}
