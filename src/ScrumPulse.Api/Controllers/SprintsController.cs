namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Entities;

/// <summary>Sprint management with proper request DTOs and production hardening.</summary>
public class SprintsController(IAppDbContext db) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Sprint>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Sprint>>> GetAll(CancellationToken ct) =>
        Ok(await db.Sprints.AsNoTracking().OrderByDescending(sprint => sprint.StartDate).ToListAsync(ct));

    [HttpPost]
    [ProducesResponseType(typeof(Sprint), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Sprint>> Create([FromBody] CreateSprintRequest request, CancellationToken ct)
    {
        if (request.EndDate < request.StartDate)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Sprint Date Range",
                Detail = "Sprint EndDate must be on or after StartDate."
            });
        }

        var sprint = new Sprint
        {
            Name = request.Name,
            Goal = request.Goal,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = request.IsActive,
            CommittedStoryPoints = request.CommittedStoryPoints,
            ConfidenceScore = request.ConfidenceScore,
            ConfidenceNotes = request.ConfidenceNotes
        };

        if (sprint.IsActive)
        {
            // Deactivate other sprints efficiently with a single update
            await db.Sprints.Where(s => s.IsActive).ExecuteUpdateAsync(
                s => s.SetProperty(e => e.IsActive, false), ct);
        }

        db.Sprints.Add(sprint);
        await db.SaveChangesAsync(ct);
        return Ok(sprint);
    }

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(typeof(Sprint), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateSprint(Guid id, CancellationToken ct)
    {
        var targetSprint = await db.Sprints.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (targetSprint == null) return NotFound();

        // Single batch update instead of loading all sprints into memory
        await db.Sprints.Where(s => s.IsActive && s.Id != id).ExecuteUpdateAsync(
            s => s.SetProperty(e => e.IsActive, false), ct);

        targetSprint.IsActive = true;
        await db.SaveChangesAsync(ct);
        return Ok(targetSprint);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Sprint), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Sprint>> Update(Guid id, [FromBody] UpdateSprintRequest request, CancellationToken ct)
    {
        var sprint = await db.Sprints.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sprint == null) return NotFound();

        if (request.EndDate < request.StartDate)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Sprint Date Range",
                Detail = "Sprint EndDate must be on or after StartDate."
            });
        }

        sprint.Name = request.Name;
        sprint.Goal = request.Goal;
        sprint.StartDate = request.StartDate;
        sprint.EndDate = request.EndDate;
        sprint.CommittedStoryPoints = request.CommittedStoryPoints;
        sprint.DeliveredStoryPoints = request.DeliveredStoryPoints;
        if (request.ConfidenceScore > 0) sprint.ConfidenceScore = request.ConfidenceScore;
        if (request.ConfidenceNotes != null) sprint.ConfidenceNotes = request.ConfidenceNotes;

        await db.SaveChangesAsync(ct);
        return Ok(sprint);
    }

    [HttpPost("{id:guid}/confidence")]
    [ProducesResponseType(typeof(Sprint), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateConfidence(Guid id, [FromQuery] int score, [FromQuery] string? notes, CancellationToken ct)
    {
        var sprint = await db.Sprints.FirstOrDefaultAsync(sprintEntity => sprintEntity.Id == id, ct);
        if (sprint == null) return NotFound();
        sprint.ConfidenceScore = Math.Clamp(score, 1, 10);
        sprint.ConfidenceNotes = notes;
        await db.SaveChangesAsync(ct);
        return Ok(sprint);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var sprint = await db.Sprints.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sprint == null) return NotFound();

        // Safely unlink all dependent entities before deleting the sprint
        await db.WorkItems.Where(w => w.SprintId == id).ExecuteUpdateAsync(s => s.SetProperty(e => e.SprintId, (Guid?)null), ct);
        await db.DailyStandups.Where(s => s.SprintId == id).ExecuteUpdateAsync(s => s.SetProperty(e => e.SprintId, (Guid?)null), ct);
        await db.Blockers.Where(b => b.SprintId == id).ExecuteUpdateAsync(s => s.SetProperty(e => e.SprintId, (Guid?)null), ct);
        await db.RetroCards.Where(r => r.SprintId == id).ExecuteUpdateAsync(s => s.SetProperty(e => e.SprintId, (Guid?)null), ct);
        await db.RetroActionItems.Where(a => a.SprintId == id).ExecuteUpdateAsync(s => s.SetProperty(e => e.SprintId, (Guid?)null), ct);
        await db.PullRequestReviewLogs.Where(p => p.SprintId == id).ExecuteUpdateAsync(s => s.SetProperty(e => e.SprintId, (Guid?)null), ct);
        await db.TechDebtItems.Where(t => t.PayoffSprintId == id).ExecuteUpdateAsync(s => s.SetProperty(e => e.PayoffSprintId, (Guid?)null), ct);

        bool wasActive = sprint.IsActive;
        db.Sprints.Remove(sprint);

        // If the deleted sprint was active, promote the latest remaining sprint
        if (wasActive)
        {
            var nextSprint = await db.Sprints
                .Where(s => s.Id != id)
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync(ct);

            if (nextSprint != null)
            {
                nextSprint.IsActive = true;
            }
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
