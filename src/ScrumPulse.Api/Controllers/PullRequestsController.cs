namespace ScrumPulse.Api.Controllers;

using System.Text;
using Microsoft.AspNetCore.Mvc;
using ScrumPulse.Api.Filters;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Mapping;
using ScrumPulse.Application.Services;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;

/// <summary>Pull request review log management with developer metrics aggregation and sync.</summary>
[Route("api/pullrequests")]
[Route("api/pull-requests")]
public class PullRequestsController(IAppDbContext db, IPullRequestSyncService? syncService = null) : BaseApiController
{
    private const double PercentageMultiplier = 100.0;


    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PullRequestLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PullRequestLogDto>>> GetAll([FromQuery] Guid? sprintId, CancellationToken ct)
    {
        var query = db.PullRequestReviewLogs
            .Include(pullRequest => pullRequest.Author)
            .Include(pullRequest => pullRequest.Reviewer)
            .Include(pullRequest => pullRequest.Sprint)
            .Include(pullRequest => pullRequest.WorkItem)
            .AsNoTracking();

        if (sprintId.HasValue) query = query.Where(pullRequest => pullRequest.SprintId == sprintId.Value);

        var list = await query.OrderByDescending(pullRequest => pullRequest.CreatedAtUtc).ToListAsync(ct);
        return Ok(list.ToDtos());
    }

    [HttpGet("developer-metrics")]
    [ProducesResponseType(typeof(IEnumerable<DeveloperPrMetricsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DeveloperPrMetricsDto>>> GetDeveloperMetrics([FromQuery] Guid? sprintId, CancellationToken ct)
    {
        var members = await db.TeamMembers.AsNoTracking().ToListAsync(ct);
        var query = db.PullRequestReviewLogs
            .Include(pullRequest => pullRequest.Author).Include(pullRequest => pullRequest.Reviewer)
            .Include(pullRequest => pullRequest.Sprint).Include(pullRequest => pullRequest.WorkItem)
            .AsNoTracking();

        if (sprintId.HasValue) query = query.Where(pullRequest => pullRequest.SprintId == sprintId.Value);

        var prLogs = await query.ToListAsync(ct);

        var metrics = members.Select(dev =>
        {
            var devPrs = prLogs.Where(pullRequest => pullRequest.AuthorId == dev.Id).ToList();
            var totalPrs = devPrs.Count;
            var totalComments = devPrs.Sum(pullRequest => pullRequest.TotalCommentsCount);
            var actionableComments = devPrs.Sum(pullRequest => pullRequest.ActionableCommentsCount);
            var actionabilityRate = totalComments > 0
                ? Math.Round(((double)actionableComments / totalComments) * PercentageMultiplier, 1)
                : 0.0;
            var avgComments = totalPrs > 0
                ? Math.Round((double)totalComments / totalPrs, 1)
                : 0.0;

            return new DeveloperPrMetricsDto(
                dev.Id, dev.Name, dev.Role.ToString(), dev.Avatar,
                totalPrs, totalComments, actionableComments, actionabilityRate, avgComments,
                devPrs.ToDtos().ToList()
            );
        }).ToList();

        return Ok(metrics);
    }

    [HttpPost]
    [ProducesResponseType(typeof(PullRequestLogDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<PullRequestLogDto>> Create([FromBody] CreatePullRequestLogRequest request, CancellationToken ct)
    {
        var reviewStatus = Enum.TryParse<ReviewStatusType>(request.ReviewStatus, true, out var parsed)
            ? parsed : ReviewStatusType.Approved;

        var log = new PullRequestReviewLog
        {
            WorkItemId = request.WorkItemId,
            AuthorId = request.AuthorId,
            ReviewerId = request.ReviewerId,
            SprintId = request.SprintId,
            PrNumber = request.PrNumber,
            PrTitle = request.PrTitle,
            PrUrl = request.PrUrl,
            TotalCommentsCount = Math.Max(0, request.TotalCommentsCount),
            ActionableCommentsCount = Math.Clamp(request.ActionableCommentsCount, 0, Math.Max(0, request.TotalCommentsCount)),
            ReviewSummary = request.ReviewSummary,
            ReviewStatus = reviewStatus,
            MergedAtUtc = reviewStatus == ReviewStatusType.Merged ? DateTime.UtcNow : null
        };

        db.PullRequestReviewLogs.Add(log);
        await db.SaveChangesAsync(ct);

        // Hydrate navigation properties for DTO
        log.Author = await db.TeamMembers.FindAsync([log.AuthorId], ct);
        if (log.ReviewerId.HasValue) log.Reviewer = await db.TeamMembers.FindAsync([log.ReviewerId.Value], ct);
        if (log.SprintId.HasValue) log.Sprint = await db.Sprints.FindAsync([log.SprintId.Value], ct);
        if (log.WorkItemId.HasValue) log.WorkItem = await db.WorkItems.FindAsync([log.WorkItemId.Value], ct);

        return CreatedAtAction(nameof(GetAll), new { id = log.Id }, log.ToDto());
    }

    [RequireScrumMaster]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var log = await db.PullRequestReviewLogs.FindAsync([id], ct);
        if (log == null) return NotFound();
        db.PullRequestReviewLogs.Remove(log);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [RequireScrumMaster]
    [HttpPost("sync")]
    [ProducesResponseType(typeof(PullRequestSyncResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PullRequestSyncResultDto>> Sync(CancellationToken ct)
    {
        if (syncService is null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented, "Pull request sync is not configured.");
        }

        try
        {
            var result = await syncService.SyncFromAzureDevOpsAsync(ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "No board connected",
                Detail = ex.Message
            });
        }
    }

    [RequireScrumMaster]
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(PullRequestSyncResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PullRequestSyncResultDto>> Import(IFormFile? file, CancellationToken ct)
    {
        if (syncService is null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented, "Pull request import is not configured.");
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "No file supplied",
                Detail = "Upload a CSV export to import pull requests."
            });
        }

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(file.ContentType, "text/csv", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Unsupported file type",
                Detail = "Only a .csv file is supported."
            });
        }

        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var csvContent = await reader.ReadToEndAsync(ct);

        var result = await syncService.ImportFromCsvAsync(csvContent, ct);
        return Ok(result);
    }

    [RequireScrumMaster]
    [HttpPost("import-text")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(PullRequestSyncResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PullRequestSyncResultDto>> ImportText([FromBody] ImportPrCsvRequest request, CancellationToken ct)
    {
        if (syncService is null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented, "Pull request import is not configured.");
        }

        if (string.IsNullOrWhiteSpace(request?.CsvContent))
        {
            return BadRequest("CSV content is required.");
        }

        var result = await syncService.ImportFromCsvAsync(request.CsvContent, ct);
        return Ok(result);
    }
}

