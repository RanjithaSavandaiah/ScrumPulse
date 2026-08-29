namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Mapping;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;

/// <summary>Pull request review log management with developer metrics aggregation.</summary>
public class PullRequestsController(IAppDbContext db) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PullRequestLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PullRequestLogDto>>> GetAll([FromQuery] Guid? sprintId, CancellationToken ct)
    {
        var query = db.PullRequestReviewLogs
            .Include(p => p.Author)
            .Include(p => p.Reviewer)
            .Include(p => p.Sprint)
            .Include(p => p.WorkItem)
            .AsNoTracking();

        if (sprintId.HasValue) query = query.Where(p => p.SprintId == sprintId.Value);

        var list = await query.OrderByDescending(p => p.CreatedAtUtc).ToListAsync(ct);
        return Ok(list.ToDtos());
    }

    [HttpGet("developer-metrics")]
    [ProducesResponseType(typeof(IEnumerable<DeveloperPrMetricsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DeveloperPrMetricsDto>>> GetDeveloperMetrics([FromQuery] Guid? sprintId, CancellationToken ct)
    {
        var members = await db.TeamMembers.AsNoTracking().ToListAsync(ct);
        var query = db.PullRequestReviewLogs
            .Include(p => p.Author).Include(p => p.Reviewer)
            .Include(p => p.Sprint).Include(p => p.WorkItem)
            .AsNoTracking();

        if (sprintId.HasValue) query = query.Where(p => p.SprintId == sprintId.Value);

        var prLogs = await query.ToListAsync(ct);

        var metrics = members.Select(dev =>
        {
            var devPrs = prLogs.Where(p => p.AuthorId == dev.Id).ToList();
            var totalPrs = devPrs.Count;
            var totalComments = devPrs.Sum(p => p.TotalCommentsCount);
            var actionableComments = devPrs.Sum(p => p.ActionableCommentsCount);
            var actionabilityRate = totalComments > 0
                ? Math.Round(((double)actionableComments / totalComments) * 100.0, 1)
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
}
