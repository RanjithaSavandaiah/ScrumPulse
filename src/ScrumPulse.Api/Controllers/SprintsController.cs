namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Entities;

/// <summary>Sprint management with DTO mapping, tenant scoping, and production hardening.</summary>
public class SprintsController(IAppDbContext db, ITenantContext? tenantContext = null) : BaseApiController
{
    private const int MinConfidenceScore = 1;
    private const int MaxConfidenceScore = 10;

    private static SprintDto ToDto(Sprint sprint) => new(
        sprint.Id, sprint.Name, sprint.Goal, sprint.StartDate, sprint.EndDate, sprint.IsActive,
        sprint.CommittedStoryPoints, sprint.DeliveredStoryPoints, sprint.ConfidenceScore,
        sprint.ConfidenceNotes, sprint.DailyWorkingHours, sprint.TeamId
    );

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SprintDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SprintDto>>> GetAll(CancellationToken ct)
    {
        var sprints = await db.Sprints
            .AsNoTracking()
            .OrderByDescending(sprint => sprint.StartDate)
            .ToListAsync(ct);

        return Ok(sprints.Select(ToDto));
    }

    [HttpPost]
    [ProducesResponseType(typeof(SprintDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SprintDto>> Create([FromBody] CreateSprintRequest request, CancellationToken ct)
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
            ConfidenceNotes = request.ConfidenceNotes,
            DailyWorkingHours = request.DailyWorkingHours,
            TeamId = tenantContext?.CurrentTeamId
        };

        if (sprint.IsActive)
        {
            // Deactivate other sprints efficiently with a single update, with fallback for non-relational test providers
            try
            {
                await db.Sprints.Where(existingSprint => existingSprint.IsActive).ExecuteUpdateAsync(
                    updateFactory => updateFactory.SetProperty(sprintEntity => sprintEntity.IsActive, false), ct);
            }
            catch (InvalidOperationException)
            {
                var activeList = await db.Sprints.Where(existingSprint => existingSprint.IsActive).ToListAsync(ct);
                foreach (var activeSprint in activeList) activeSprint.IsActive = false;
            }
        }

        db.Sprints.Add(sprint);
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(sprint));
    }

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(typeof(SprintDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateSprint(Guid id, CancellationToken ct)
    {
        var targetSprint = await db.Sprints.FirstOrDefaultAsync(sprint => sprint.Id == id, ct);
        if (targetSprint == null) return NotFound();

        // Single batch update instead of loading all sprints into memory
        await db.Sprints.Where(existingSprint => existingSprint.IsActive && existingSprint.Id != id).ExecuteUpdateAsync(
            updateFactory => updateFactory.SetProperty(sprintEntity => sprintEntity.IsActive, false), ct);

        targetSprint.IsActive = true;
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(targetSprint));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SprintDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SprintDto>> Update(Guid id, [FromBody] UpdateSprintRequest request, CancellationToken ct)
    {
        var sprint = await db.Sprints.FirstOrDefaultAsync(sprintEntity => sprintEntity.Id == id, ct);
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
        if (request.DailyWorkingHours > 0) sprint.DailyWorkingHours = request.DailyWorkingHours;

        await db.SaveChangesAsync(ct);
        return Ok(ToDto(sprint));
    }

    [HttpPost("{id:guid}/confidence")]
    [ProducesResponseType(typeof(SprintDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateConfidence(Guid id, [FromQuery] int score, [FromQuery] string? notes, CancellationToken ct)
    {
        var sprint = await db.Sprints.FirstOrDefaultAsync(sprintEntity => sprintEntity.Id == id, ct);
        if (sprint == null) return NotFound();
        sprint.ConfidenceScore = Math.Clamp(score, MinConfidenceScore, MaxConfidenceScore);
        sprint.ConfidenceNotes = notes;
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(sprint));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var sprint = await db.Sprints.FirstOrDefaultAsync(sprintEntity => sprintEntity.Id == id, ct);
        if (sprint == null) return NotFound();

        // Safely unlink all dependent entities before deleting the sprint
        try
        {
            await db.WorkItems.Where(workItem => workItem.SprintId == id).ExecuteUpdateAsync(updateFactory => updateFactory.SetProperty(workItem => workItem.SprintId, (Guid?)null), ct);
            await db.DailyStandups.Where(standup => standup.SprintId == id).ExecuteUpdateAsync(updateFactory => updateFactory.SetProperty(standup => standup.SprintId, (Guid?)null), ct);
            await db.Blockers.Where(blocker => blocker.SprintId == id).ExecuteUpdateAsync(updateFactory => updateFactory.SetProperty(blocker => blocker.SprintId, (Guid?)null), ct);
            await db.RetroCards.Where(retroCard => retroCard.SprintId == id).ExecuteUpdateAsync(updateFactory => updateFactory.SetProperty(retroCard => retroCard.SprintId, (Guid?)null), ct);
            await db.RetroActionItems.Where(actionItem => actionItem.SprintId == id).ExecuteUpdateAsync(updateFactory => updateFactory.SetProperty(actionItem => actionItem.SprintId, (Guid?)null), ct);
            await db.PullRequestReviewLogs.Where(pullRequest => pullRequest.SprintId == id).ExecuteUpdateAsync(updateFactory => updateFactory.SetProperty(pullRequest => pullRequest.SprintId, (Guid?)null), ct);
            await db.TechDebtItems.Where(techDebt => techDebt.PayoffSprintId == id).ExecuteUpdateAsync(updateFactory => updateFactory.SetProperty(techDebt => techDebt.PayoffSprintId, (Guid?)null), ct);
        }
        catch (InvalidOperationException)
        {
            var workItems = await db.WorkItems.Where(workItem => workItem.SprintId == id).ToListAsync(ct);
            foreach (var workItem in workItems) workItem.SprintId = null;

            var standups = await db.DailyStandups.Where(standup => standup.SprintId == id).ToListAsync(ct);
            foreach (var standup in standups) standup.SprintId = null;

            var blockers = await db.Blockers.Where(blocker => blocker.SprintId == id).ToListAsync(ct);
            foreach (var blocker in blockers) blocker.SprintId = null;

            var retros = await db.RetroCards.Where(retroCard => retroCard.SprintId == id).ToListAsync(ct);
            foreach (var retroCard in retros) retroCard.SprintId = null;

            var actions = await db.RetroActionItems.Where(actionItem => actionItem.SprintId == id).ToListAsync(ct);
            foreach (var actionItem in actions) actionItem.SprintId = null;

            var prs = await db.PullRequestReviewLogs.Where(pullRequest => pullRequest.SprintId == id).ToListAsync(ct);
            foreach (var pullRequest in prs) pullRequest.SprintId = null;

            var debts = await db.TechDebtItems.Where(techDebt => techDebt.PayoffSprintId == id).ToListAsync(ct);
            foreach (var techDebt in debts) techDebt.PayoffSprintId = null;
        }

        bool wasActive = sprint.IsActive;
        db.Sprints.Remove(sprint);

        // If the deleted sprint was active, promote the latest remaining sprint
        if (wasActive)
        {
            var nextSprint = await db.Sprints
                .Where(existingSprint => existingSprint.Id != id)
                .OrderByDescending(existingSprint => existingSprint.StartDate)
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
