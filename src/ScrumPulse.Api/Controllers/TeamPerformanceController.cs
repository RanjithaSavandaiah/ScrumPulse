namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Services;

/// <summary>
/// Team performance and growth metrics for client-facing presentations.
/// Provides aggregated cross sprint KPIs, auto-generated highlights, and trend data.
/// Guarded with defensive try catch error handling to protect against 500 failures.
/// </summary>
[Route("api/[controller]")]
[Route("api/team-performance")]
public class TeamPerformanceController(
    ITeamPerformanceService performanceService,
    ILogger<TeamPerformanceController>? logger = null) : BaseApiController
{
    /// <summary>Full performance summary with all metrics, highlights, and engagement data.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(TeamPerformanceSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamPerformanceSummaryDto>> GetSummary(
        [FromQuery] int sprintCount = 6,
        CancellationToken ct = default)
    {
        try
        {
            var result = await performanceService.GetPerformanceSummaryAsync(sprintCount, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Unhandled exception in TeamPerformanceController.GetSummary: {Message}", ex.Message);

            var fallbackSummary = new TeamPerformanceSummaryDto(
                "FikaCoders",
                "N/A",
                0,
                "No completed sprint telemetry available to analyze team performance yet.",
                0,
                DateTime.UtcNow,
                [],
                [],
                [],
                new TeamEngagementDto(0, 0, 0, 0, 0, 0, "No Data")
            );

            return Ok(fallbackSummary);
        }
    }

    /// <summary>Client ready highlight bullet points only.</summary>
    [HttpGet("highlights")]
    [ProducesResponseType(typeof(IReadOnlyList<TeamHighlightDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TeamHighlightDto>>> GetHighlights(
        [FromQuery] int sprintCount = 6,
        CancellationToken ct = default)
    {
        try
        {
            var result = await performanceService.GetHighlightsAsync(sprintCount, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Unhandled exception in TeamPerformanceController.GetHighlights: {Message}", ex.Message);
            return Ok(Array.Empty<TeamHighlightDto>());
        }
    }

    /// <summary>Sprint by sprint growth trend data for charts.</summary>
    [HttpGet("growth-trend")]
    [ProducesResponseType(typeof(IReadOnlyList<SprintGrowthSnapshotDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SprintGrowthSnapshotDto>>> GetGrowthTrend(
        [FromQuery] int sprintCount = 8,
        CancellationToken ct = default)
    {
        try
        {
            var result = await performanceService.GetGrowthTrendAsync(sprintCount, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Unhandled exception in TeamPerformanceController.GetGrowthTrend: {Message}", ex.Message);
            return Ok(Array.Empty<SprintGrowthSnapshotDto>());
        }
    }
}
