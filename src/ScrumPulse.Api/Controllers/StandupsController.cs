namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Mapping;
using ScrumPulse.Domain.Entities;

/// <summary>Daily standup management with protected admin endpoints.</summary>
public class StandupsController(IAppDbContext db) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DailyStandupDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DailyStandupDto>>> GetAll(
        [FromQuery] Guid? sprintId = null,
        [FromQuery] Guid? memberId = null,
        [FromQuery] DateTime? date = null,
        CancellationToken ct = default)
    {
        var query = db.DailyStandups
            .Include(standup => standup.TeamMember)
            .AsQueryable();

        if (sprintId.HasValue) query = query.Where(s => s.SprintId == sprintId.Value);
        if (memberId.HasValue) query = query.Where(s => s.TeamMemberId == memberId.Value);
        if (date.HasValue)
        {
            var targetDate = date.Value.Date;
            query = query.Where(s => s.StandupDate.Date == targetDate);
        }

        var list = await query
            .OrderByDescending(standup => standup.StandupDate)
            .ThenByDescending(standup => standup.CreatedAtUtc)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(list.ToDtos());
    }

    [HttpPost]
    [ProducesResponseType(typeof(DailyStandupDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DailyStandupDto>> Submit([FromBody] SubmitStandupRequest request, CancellationToken ct)
    {
        var standup = new DailyStandup
        {
            TeamMemberId = request.TeamMemberId,
            SprintId = request.SprintId,
            YesterdaySummary = request.YesterdaySummary,
            TodayPlan = request.TodayPlan,
            BlockersText = request.BlockersText ?? "None",
            MoodScore = request.MoodScore,
            StandupDate = DateTime.UtcNow
        };
        db.DailyStandups.Add(standup);
        await db.SaveChangesAsync(ct);

        var member = await db.TeamMembers.FirstOrDefaultAsync(teamMember => teamMember.Id == request.TeamMemberId, ct);
        standup.TeamMember = member;

        return Ok(standup.ToDto());
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DailyStandupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DailyStandupDto>> Update(Guid id, [FromBody] SubmitStandupRequest request, CancellationToken ct)
    {
        var standup = await db.DailyStandups.FindAsync([id], ct);
        if (standup == null) return NotFound();

        standup.TeamMemberId = request.TeamMemberId;
        if (request.SprintId.HasValue) standup.SprintId = request.SprintId;
        standup.YesterdaySummary = request.YesterdaySummary;
        standup.TodayPlan = request.TodayPlan;
        standup.BlockersText = request.BlockersText ?? "None";
        standup.MoodScore = request.MoodScore;

        await db.SaveChangesAsync(ct);

        var member = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == request.TeamMemberId, ct);
        standup.TeamMember = member;

        return Ok(standup.ToDto());
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var item = await db.DailyStandups.FindAsync([id], ct);
        if (item == null) return NotFound();
        db.DailyStandups.Remove(item);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Administrative endpoint to clear all standup data.
    /// Protected — requires the X-Admin-Key header matching the configured SM_PIN.
    /// </summary>
    [HttpDelete("clear-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ClearAll(
        [FromHeader(Name = "X-Admin-Key")] string? adminKey,
        [FromServices] IConfiguration configuration,
        CancellationToken ct)
    {
        // Require admin key to prevent accidental/malicious mass deletion
        var configuredPin = Environment.GetEnvironmentVariable("SM_PIN")
            ?? configuration["Auth:ScrumMasterPin"];

        if (string.IsNullOrWhiteSpace(adminKey) || adminKey != configuredPin)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Admin key required for bulk deletion." });
        }

        var items = await db.DailyStandups.ToListAsync(ct);
        db.DailyStandups.RemoveRange(items);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
