namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Entities;

public class StandupsController(IAppDbContext db) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DailyStandupDto>>> GetAll(
        [FromQuery] Guid? sprintId = null,
        [FromQuery] Guid? memberId = null,
        [FromQuery] DateTime? date = null)
    {
        var query = db.DailyStandups
            .Include(standup => standup.TeamMember)
            .AsQueryable();

        if (sprintId.HasValue)
        {
            query = query.Where(s => s.SprintId == sprintId.Value);
        }

        if (memberId.HasValue)
        {
            query = query.Where(s => s.TeamMemberId == memberId.Value);
        }

        if (date.HasValue)
        {
            var targetDate = date.Value.Date;
            query = query.Where(s => s.StandupDate.Date == targetDate);
        }

        var list = await query
            .OrderByDescending(standup => standup.StandupDate)
            .ThenByDescending(standup => standup.CreatedAtUtc)
            .ToListAsync();

        return Ok(list.Select(standup => new DailyStandupDto(
            standup.Id,
            standup.TeamMemberId,
            standup.TeamMember?.Name ?? "Member",
            standup.TeamMember?.Avatar ?? "",
            standup.StandupDate,
            standup.YesterdaySummary,
            standup.TodayPlan,
            standup.BlockersText,
            standup.MoodScore,
            standup.SprintId
        )));
    }

    [HttpPost]
    public async Task<ActionResult<DailyStandupDto>> Submit([FromBody] SubmitStandupRequest request)
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
        await db.SaveChangesAsync();

        var member = await db.TeamMembers.FirstOrDefaultAsync(teamMember => teamMember.Id == request.TeamMemberId);

        return Ok(new DailyStandupDto(
            standup.Id,
            standup.TeamMemberId,
            member?.Name ?? "Member",
            member?.Avatar ?? "",
            standup.StandupDate,
            standup.YesterdaySummary,
            standup.TodayPlan,
            standup.BlockersText,
            standup.MoodScore,
            standup.SprintId
        ));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DailyStandupDto>> Update(Guid id, [FromBody] SubmitStandupRequest request)
    {
        var standup = await db.DailyStandups.FindAsync(id);
        if (standup == null) return NotFound();

        standup.TeamMemberId = request.TeamMemberId;
        if (request.SprintId.HasValue) standup.SprintId = request.SprintId;
        standup.YesterdaySummary = request.YesterdaySummary;
        standup.TodayPlan = request.TodayPlan;
        standup.BlockersText = request.BlockersText ?? "None";
        standup.MoodScore = request.MoodScore;

        await db.SaveChangesAsync();

        var member = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == request.TeamMemberId);

        return Ok(new DailyStandupDto(
            standup.Id,
            standup.TeamMemberId,
            member?.Name ?? "Member",
            member?.Avatar ?? "",
            standup.StandupDate,
            standup.YesterdaySummary,
            standup.TodayPlan,
            standup.BlockersText,
            standup.MoodScore,
            standup.SprintId
        ));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await db.DailyStandups.FindAsync(id);
        if (item == null) return NotFound();
        db.DailyStandups.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("clear-all")]
    public async Task<IActionResult> ClearAll()
    {
        var items = await db.DailyStandups.ToListAsync();
        db.DailyStandups.RemoveRange(items);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
