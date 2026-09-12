namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Services;
using Microsoft.Extensions.Logging;

[Route("api/[controller]")]
[Route("api/executive-reports")]
public class ExecutiveReportsController : BaseApiController
{
    private const int DefaultVelocityTrendSprintCount = 6;
    private readonly IMetricsCalculatorService _metricsCalculatorService;
    private readonly IExportService _exportService;
    private readonly ILogger<ExecutiveReportsController>? _logger;

    [ActivatorUtilitiesConstructor]
    public ExecutiveReportsController(
        IMetricsCalculatorService metricsCalculatorService,
        IExportService exportService,
        ILogger<ExecutiveReportsController>? logger = null)
    {
        _metricsCalculatorService = metricsCalculatorService;
        _exportService = exportService;
        _logger = logger;
    }

    /// <summary>Testing constructor providing backward compatibility for direct DbContext tests.</summary>
    public ExecutiveReportsController(
        IMetricsCalculatorService metricsCalculatorService,
        IAppDbContext db,
        ILogger<ExecutiveReportsController>? logger = null)
        : this(metricsCalculatorService, new ScrumPulse.Infrastructure.Services.ExportService(db), logger)
    {
    }

    [HttpGet("sprint/{sprintId:guid}")]
    public async Task<ActionResult<ExecutiveReportDto>> GetSprintReport(Guid sprintId, CancellationToken ct = default) =>
        Ok(await _metricsCalculatorService.GenerateExecutiveReportAsync(sprintId, ct));

    [HttpGet("velocity-trend")]
    [ProducesResponseType(typeof(SprintVelocityTrendDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SprintVelocityTrendDto>> GetVelocityTrend([FromQuery] int count = DefaultVelocityTrendSprintCount, CancellationToken ct = default) =>
        Ok(await _metricsCalculatorService.GetVelocityTrendAsync(count, ct));

    [HttpGet("sprint/{sprintId:guid}/health")]
    [ProducesResponseType(typeof(SprintHealthDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SprintHealthDto>> GetSprintHealth(Guid sprintId, CancellationToken ct = default) =>
        Ok(await _metricsCalculatorService.CalculateSprintHealthAsync(sprintId, ct));

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
            var result = await _metricsCalculatorService.CompareSprintsAsync(sprintA, sprintB, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            _logger?.LogWarning(ex, "Sprint comparison target not found (sprintA={SprintA}, sprintB={SprintB}): {Message}", sprintA, sprintB, ex.Message);
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("sprint/{sprintId:guid}/export-csv")]
    public async Task<IActionResult> ExportSprintCsv(Guid sprintId, CancellationToken ct = default)
    {
        var result = await _exportService.ExportSprintCsvAsync(sprintId, ct);
        if (result == null) return NotFound();
        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpGet("export-json")]
    public async Task<IActionResult> ExportJson(CancellationToken ct = default)
    {
        var result = await _exportService.ExportEnterpriseJsonAsync(ct);
        return File(result.Content, result.ContentType, result.FileName);
    }
}
