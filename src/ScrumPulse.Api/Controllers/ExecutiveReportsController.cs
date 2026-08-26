namespace ScrumPulse.Api.Controllers;

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Services;

public class ExecutiveReportsController(IMetricsCalculatorService metricsCalculatorService, IAppDbContext db) : BaseApiController
{
    [HttpGet("sprint/{sprintId:guid}")]
    public async Task<ActionResult<ExecutiveReportDto>> GetSprintReport(Guid sprintId) =>
        Ok(await metricsCalculatorService.GenerateExecutiveReportAsync(sprintId));

    [HttpGet("export-json")]
    public async Task<IActionResult> ExportJson()
    {
        var sprints = await db.Sprints.Include(sprint => sprint.WorkItems).ToListAsync();
        var members = await db.TeamMembers.ToListAsync();
        var blockers = await db.Blockers.ToListAsync();
        var feedbacks = await db.Monthly1on1Feedbacks.ToListAsync();
        var kudos = await db.KudosCards.ToListAsync();

        var bundle = new
        {
            ExportedAtUtc = DateTime.UtcNow,
            Platform = "ScrumPulse Enterprise",
            Sprints = sprints,
            TeamMembers = members,
            Blockers = blockers,
            MonthlyFeedbacks = feedbacks,
            Kudos = kudos
        };

        var json = JsonSerializer.Serialize(bundle, new JsonSerializerOptions { WriteIndented = true });
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", $"ScrumPulse_Export_{DateTime.UtcNow:yyyyMMdd}.json");
    }
}
