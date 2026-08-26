namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Domain.Entities;

public class SprintsController(IAppDbContext db) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Sprint>>> GetAll() =>
        Ok(await db.Sprints.OrderByDescending(sprint => sprint.StartDate).ToListAsync());

    [HttpPost]
    public async Task<ActionResult<Sprint>> Create([FromBody] Sprint sprint)
    {
        if (sprint.EndDate < sprint.StartDate)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Sprint Date Range",
                Detail = "Sprint EndDate must be on or after StartDate."
            });
        }

        if (sprint.IsActive)
        {
            var activeSprints = await db.Sprints.Where(s => s.IsActive).ToListAsync();
            foreach (var active in activeSprints)
            {
                active.IsActive = false;
            }
        }

        db.Sprints.Add(sprint);
        await db.SaveChangesAsync();
        return Ok(sprint);
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> ActivateSprint(Guid id)
    {
        var targetSprint = await db.Sprints.FirstOrDefaultAsync(s => s.Id == id);
        if (targetSprint == null) return NotFound();

        var allSprints = await db.Sprints.ToListAsync();
        foreach (var sprint in allSprints)
        {
            sprint.IsActive = (sprint.Id == id);
        }

        await db.SaveChangesAsync();
        return Ok(targetSprint);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Sprint>> Update(Guid id, [FromBody] Sprint update)
    {
        var sprint = await db.Sprints.FirstOrDefaultAsync(s => s.Id == id);
        if (sprint == null) return NotFound();

        if (update.EndDate < update.StartDate)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Sprint Date Range",
                Detail = "Sprint EndDate must be on or after StartDate."
            });
        }

        sprint.Name = update.Name;
        sprint.Goal = update.Goal;
        sprint.StartDate = update.StartDate;
        sprint.EndDate = update.EndDate;
        sprint.CommittedStoryPoints = update.CommittedStoryPoints;
        sprint.DeliveredStoryPoints = update.DeliveredStoryPoints;
        if (update.ConfidenceScore > 0) sprint.ConfidenceScore = update.ConfidenceScore;
        if (update.ConfidenceNotes != null) sprint.ConfidenceNotes = update.ConfidenceNotes;

        await db.SaveChangesAsync();
        return Ok(sprint);
    }

    [HttpPost("{id:guid}/confidence")]
    public async Task<IActionResult> UpdateConfidence(Guid id, [FromQuery] int score, [FromQuery] string? notes)
    {
        var sprint = await db.Sprints.FirstOrDefaultAsync(sprintEntity => sprintEntity.Id == id);
        if (sprint == null) return NotFound();
        sprint.ConfidenceScore = Math.Clamp(score, 1, 10);
        sprint.ConfidenceNotes = notes;
        await db.SaveChangesAsync();
        return Ok(sprint);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var sprint = await db.Sprints.FirstOrDefaultAsync(s => s.Id == id);
        if (sprint == null) return NotFound();

        // Safely unlink all dependent entities before deleting the sprint
        var workItems = await db.WorkItems.Where(w => w.SprintId == id).ToListAsync();
        foreach (var w in workItems) w.SprintId = null;

        var standups = await db.DailyStandups.Where(s => s.SprintId == id).ToListAsync();
        foreach (var s in standups) s.SprintId = null;

        var blockers = await db.Blockers.Where(b => b.SprintId == id).ToListAsync();
        foreach (var b in blockers) b.SprintId = null;

        var retroCards = await db.RetroCards.Where(r => r.SprintId == id).ToListAsync();
        foreach (var r in retroCards) r.SprintId = null;

        var retroActions = await db.RetroActionItems.Where(a => a.SprintId == id).ToListAsync();
        foreach (var a in retroActions) a.SprintId = null;

        var prLogs = await db.PullRequestReviewLogs.Where(p => p.SprintId == id).ToListAsync();
        foreach (var p in prLogs) p.SprintId = null;

        var techDebts = await db.TechDebtItems.Where(t => t.PayoffSprintId == id).ToListAsync();
        foreach (var t in techDebts) t.PayoffSprintId = null;

        bool wasActive = sprint.IsActive;
        db.Sprints.Remove(sprint);

        // If the deleted sprint was active, promote the latest remaining sprint to active
        if (wasActive)
        {
            var nextSprint = await db.Sprints
                .Where(s => s.Id != id)
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync();

            if (nextSprint != null)
            {
                nextSprint.IsActive = true;
            }
        }

        await db.SaveChangesAsync();
        return NoContent();
    }
}
