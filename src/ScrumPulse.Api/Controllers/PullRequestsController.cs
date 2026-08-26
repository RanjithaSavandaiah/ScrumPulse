namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Entities;

public class PullRequestsController(IAppDbContext db) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PullRequestLogDto>>> GetAll([FromQuery] Guid? sprintId)
    {
        var query = db.PullRequestReviewLogs
            .Include(p => p.Author)
            .Include(p => p.Reviewer)
            .Include(p => p.Sprint)
            .Include(p => p.WorkItem)
            .AsNoTracking();

        if (sprintId.HasValue)
        {
            query = query.Where(p => p.SprintId == sprintId.Value);
        }

        var list = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new PullRequestLogDto(
                p.Id,
                p.WorkItemId,
                p.WorkItem != null ? p.WorkItem.Title : null,
                p.AuthorId,
                p.Author != null ? p.Author.Name : "Unknown",
                p.ReviewerId,
                p.Reviewer != null ? p.Reviewer.Name : null,
                p.SprintId,
                p.Sprint != null ? p.Sprint.Name : null,
                p.PrNumber,
                p.PrTitle,
                p.PrUrl,
                p.TotalCommentsCount,
                p.ActionableCommentsCount,
                p.ReviewSummary,
                p.ReviewStatus,
                p.CreatedAtUtc,
                p.MergedAtUtc
            ))
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("developer-metrics")]
    public async Task<ActionResult<IEnumerable<DeveloperPrMetricsDto>>> GetDeveloperMetrics([FromQuery] Guid? sprintId)
    {
        var members = await db.TeamMembers.AsNoTracking().ToListAsync();
        var query = db.PullRequestReviewLogs
            .Include(p => p.Author)
            .Include(p => p.Reviewer)
            .Include(p => p.Sprint)
            .Include(p => p.WorkItem)
            .AsNoTracking();

        if (sprintId.HasValue)
        {
            query = query.Where(p => p.SprintId == sprintId.Value);
        }

        var prLogs = await query.ToListAsync();

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

            var prDtos = devPrs.Select(p => new PullRequestLogDto(
                p.Id,
                p.WorkItemId,
                p.WorkItem?.Title,
                p.AuthorId,
                dev.Name,
                p.ReviewerId,
                p.Reviewer?.Name,
                p.SprintId,
                p.Sprint?.Name,
                p.PrNumber,
                p.PrTitle,
                p.PrUrl,
                p.TotalCommentsCount,
                p.ActionableCommentsCount,
                p.ReviewSummary,
                p.ReviewStatus,
                p.CreatedAtUtc,
                p.MergedAtUtc
            )).ToList();

            return new DeveloperPrMetricsDto(
                dev.Id,
                dev.Name,
                dev.Role.ToString(),
                dev.Avatar,
                totalPrs,
                totalComments,
                actionableComments,
                actionabilityRate,
                avgComments,
                prDtos
            );
        }).ToList();

        return Ok(metrics);
    }

    [HttpPost]
    public async Task<ActionResult<PullRequestLogDto>> Create([FromBody] CreatePullRequestLogRequest request)
    {
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
            ReviewStatus = string.IsNullOrWhiteSpace(request.ReviewStatus) ? "Approved" : request.ReviewStatus,
            MergedAtUtc = request.ReviewStatus == "Merged" ? DateTime.UtcNow : null
        };

        db.PullRequestReviewLogs.Add(log);
        await db.SaveChangesAsync();

        var author = await db.TeamMembers.FindAsync(log.AuthorId);
        var reviewer = log.ReviewerId.HasValue ? await db.TeamMembers.FindAsync(log.ReviewerId.Value) : null;
        var sprint = log.SprintId.HasValue ? await db.Sprints.FindAsync(log.SprintId.Value) : null;
        var workItem = log.WorkItemId.HasValue ? await db.WorkItems.FindAsync(log.WorkItemId.Value) : null;

        var dto = new PullRequestLogDto(
            log.Id,
            log.WorkItemId,
            workItem?.Title,
            log.AuthorId,
            author?.Name ?? "Unknown",
            log.ReviewerId,
            reviewer?.Name,
            log.SprintId,
            sprint?.Name,
            log.PrNumber,
            log.PrTitle,
            log.PrUrl,
            log.TotalCommentsCount,
            log.ActionableCommentsCount,
            log.ReviewSummary,
            log.ReviewStatus,
            log.CreatedAtUtc,
            log.MergedAtUtc
        );

        return CreatedAtAction(nameof(GetAll), new { id = log.Id }, dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var log = await db.PullRequestReviewLogs.FindAsync(id);
        if (log == null) return NotFound();

        db.PullRequestReviewLogs.Remove(log);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
