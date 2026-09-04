namespace ScrumPulse.Api.Controllers;

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Services;

[Route("api/[controller]")]
[Route("api/executive-reports")]
public class ExecutiveReportsController(IMetricsCalculatorService metricsCalculatorService, IAppDbContext db) : BaseApiController
{
    [HttpGet("sprint/{sprintId:guid}")]
    public async Task<ActionResult<ExecutiveReportDto>> GetSprintReport(Guid sprintId, CancellationToken ct = default) =>
        Ok(await metricsCalculatorService.GenerateExecutiveReportAsync(sprintId, ct));

    [HttpGet("velocity-trend")]
    [ProducesResponseType(typeof(SprintVelocityTrendDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SprintVelocityTrendDto>> GetVelocityTrend([FromQuery] int count = 6, CancellationToken ct = default) =>
        Ok(await metricsCalculatorService.GetVelocityTrendAsync(count, ct));

    [HttpGet("sprint/{sprintId:guid}/health")]
    [ProducesResponseType(typeof(SprintHealthDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SprintHealthDto>> GetSprintHealth(Guid sprintId, CancellationToken ct = default) =>
        Ok(await metricsCalculatorService.CalculateSprintHealthAsync(sprintId, ct));

    [HttpGet("compare")]
    [ProducesResponseType(typeof(SprintComparisonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SprintComparisonDto>> CompareSprints(
        [FromQuery] Guid sprintA,
        [FromQuery] Guid sprintB,
        CancellationToken ct = default)
    {
        try
        {
            var result = await metricsCalculatorService.CompareSprintsAsync(sprintA, sprintB, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("sprint/{sprintId:guid}/export-csv")]
    public async Task<IActionResult> ExportSprintCsv(Guid sprintId, CancellationToken ct = default)
    {
        var sprint = await db.Sprints
            .Include(s => s.WorkItems)
                .ThenInclude(w => w.Assignee)
            .FirstOrDefaultAsync(s => s.Id == sprintId, ct);

        if (sprint == null) return NotFound();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Key,Title,Type,Status,Priority,StoryPoints,Assignee,DevCycleHours,PrReviewLatencyHours,TotalCycleHours,IsEscapedDefect,DaysInStatus");

        foreach (var item in sprint.WorkItems.OrderBy(w => w.Key))
        {
            var cleanTitle = item.Title.Replace("\"", "\"\"");
            var cleanAssignee = (item.Assignee?.Name ?? "Unassigned").Replace("\"", "\"\"");
            sb.AppendLine($"\"{item.Key}\",\"{cleanTitle}\",{item.Type},{item.Status},{item.Priority},{item.StoryPoints},\"{cleanAssignee}\",{item.DevCycleTimeHours ?? 0},{item.PrReviewLatencyHours ?? 0},{item.TotalCycleTimeHours ?? 0},{item.IsEscapedDefect},{item.DaysInCurrentStatus}");
        }

        var preamble = System.Text.Encoding.UTF8.GetPreamble();
        var bytes = preamble.Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var cleanSprintName = System.Text.RegularExpressions.Regex.Replace(sprint.Name, @"[^a-zA-Z0-9_\-]", "_");

        return File(bytes, "text/csv", $"{cleanSprintName}_Report_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("export-json")]
    public async Task<IActionResult> ExportJson(CancellationToken ct = default)
    {
        var sprints = await db.Sprints.Include(sprint => sprint.WorkItems).ToListAsync(ct);
        var members = await db.TeamMembers.ToListAsync(ct);
        var blockers = await db.Blockers.ToListAsync(ct);
        var feedbacks = await db.Monthly1on1Feedbacks.ToListAsync(ct);
        var kudos = await db.KudosCards.ToListAsync(ct);
        var leaves = await db.TeamLeaves.ToListAsync(ct);
        var standups = await db.DailyStandups.ToListAsync(ct);

        var bundle = new
        {
            ExportedAtUtc = DateTime.UtcNow,
            Platform = "ScrumPulse Enterprise",
            Sprints = sprints,
            TeamMembers = members,
            Blockers = blockers,
            MonthlyFeedbacks = feedbacks,
            Kudos = kudos,
            Leaves = leaves,
            DailyStandups = standups
        };

        var json = JsonSerializer.Serialize(bundle, new JsonSerializerOptions { WriteIndented = true });
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", $"ScrumPulse_Export_{DateTime.UtcNow:yyyyMMdd}.json");
    }
}
