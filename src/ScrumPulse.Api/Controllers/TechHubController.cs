namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Domain.Entities;

public class TechHubController(IAppDbContext db) : BaseApiController
{
    [HttpGet("debt")]
    [HttpGet("tech-debt")]
    public async Task<ActionResult<IEnumerable<object>>> GetTechDebt()
    {
        var list = await db.TechDebtItems
            .Include(t => t.Assignee)
            .OrderByDescending(techDebtItem => techDebtItem.CreatedAtUtc)
            .ToListAsync();

        return Ok(list.Select(t => new
        {
            t.Id,
            t.Title,
            t.Description,
            t.Severity,
            t.EstimatedHours,
            t.Status,
            t.PayoffSprintId,
            t.AssigneeId,
            AssigneeName = t.Assignee?.Name,
            t.CreatedAtUtc
        }));
    }

    [HttpPost("debt")]
    [HttpPost("tech-debt")]
    public async Task<ActionResult<object>> CreateTechDebt([FromBody] TechDebtItem item)
    {
        db.TechDebtItems.Add(item);
        await db.SaveChangesAsync();

        var assignee = item.AssigneeId.HasValue
            ? await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == item.AssigneeId.Value)
            : null;

        return Ok(new
        {
            item.Id,
            item.Title,
            item.Description,
            item.Severity,
            item.EstimatedHours,
            item.Status,
            item.PayoffSprintId,
            item.AssigneeId,
            AssigneeName = assignee?.Name,
            item.CreatedAtUtc
        });
    }

    [HttpPost("debt/{id:guid}/resolve")]
    [HttpPost("tech-debt/{id:guid}/resolve")]
    public async Task<ActionResult<object>> ResolveTechDebt(Guid id, [FromBody] TechDebtItem? update)
    {
        var item = await db.TechDebtItems.Include(t => t.Assignee).FirstOrDefaultAsync(t => t.Id == id);
        if (item == null) return NotFound();
        item.Status = update?.Status ?? "Resolved";
        await db.SaveChangesAsync();

        return Ok(new
        {
            item.Id,
            item.Title,
            item.Description,
            item.Severity,
            item.EstimatedHours,
            item.Status,
            item.PayoffSprintId,
            item.AssigneeId,
            AssigneeName = item.Assignee?.Name,
            item.CreatedAtUtc
        });
    }

    [HttpPut("debt/{id:guid}")]
    [HttpPut("tech-debt/{id:guid}")]
    public async Task<ActionResult<object>> UpdateTechDebt(Guid id, [FromBody] TechDebtItem update)
    {
        var item = await db.TechDebtItems.Include(t => t.Assignee).FirstOrDefaultAsync(t => t.Id == id);
        if (item == null) return NotFound();
        item.Title = update.Title;
        item.Description = update.Description;
        item.Severity = update.Severity;
        item.EstimatedHours = update.EstimatedHours;
        item.Status = update.Status;
        item.PayoffSprintId = update.PayoffSprintId;
        item.AssigneeId = update.AssigneeId;
        await db.SaveChangesAsync();

        var assignee = item.AssigneeId.HasValue
            ? await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == item.AssigneeId.Value)
            : null;

        return Ok(new
        {
            item.Id,
            item.Title,
            item.Description,
            item.Severity,
            item.EstimatedHours,
            item.Status,
            item.PayoffSprintId,
            item.AssigneeId,
            AssigneeName = assignee?.Name,
            item.CreatedAtUtc
        });
    }

    [HttpDelete("debt/{id:guid}")]
    [HttpDelete("tech-debt/{id:guid}")]
    public async Task<IActionResult> DeleteTechDebt(Guid id)
    {
        var item = await db.TechDebtItems.FindAsync(id);
        if (item == null) return NotFound();
        db.TechDebtItems.Remove(item);
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("talks")]
    [HttpGet("tech-talks")]
    public async Task<ActionResult<IEnumerable<object>>> GetTechTalks()
    {
        var list = await db.TechTalkLogs.Include(talk => talk.Presenter).OrderByDescending(talk => talk.TalkDate).ToListAsync();
        return Ok(list.Select(talk => new
        {
            talk.Id, talk.Topic, talk.PresenterId, PresenterName = talk.Presenter?.Name,
            talk.TalkDate, talk.DurationMinutes, talk.KeyTakeaways, talk.SlidesUrl
        }));
    }

    [HttpPost("talks")]
    [HttpPost("tech-talks")]
    public async Task<ActionResult<object>> CreateTechTalk([FromBody] TechTalkLog log)
    {
        db.TechTalkLogs.Add(log);
        await db.SaveChangesAsync();
        var presenter = await db.TeamMembers.FindAsync(log.PresenterId);
        return Ok(new
        {
            log.Id, log.Topic, log.PresenterId, PresenterName = presenter?.Name,
            log.TalkDate, log.DurationMinutes, log.KeyTakeaways, log.SlidesUrl
        });
    }

    [HttpPut("talks/{id:guid}")]
    [HttpPut("tech-talks/{id:guid}")]
    public async Task<ActionResult<object>> UpdateTechTalk(Guid id, [FromBody] TechTalkLog update)
    {
        var talk = await db.TechTalkLogs.FindAsync(id);
        if (talk == null) return NotFound();
        talk.Topic = update.Topic;
        talk.PresenterId = update.PresenterId;
        talk.TalkDate = update.TalkDate;
        talk.DurationMinutes = update.DurationMinutes;
        talk.KeyTakeaways = update.KeyTakeaways;
        talk.SlidesUrl = update.SlidesUrl;
        await db.SaveChangesAsync();

        var presenter = await db.TeamMembers.FindAsync(talk.PresenterId);
        return Ok(new
        {
            talk.Id, talk.Topic, talk.PresenterId, PresenterName = presenter?.Name,
            talk.TalkDate, talk.DurationMinutes, talk.KeyTakeaways, talk.SlidesUrl
        });
    }

    [HttpDelete("talks/{id:guid}")]
    [HttpDelete("tech-talks/{id:guid}")]
    public async Task<IActionResult> DeleteTechTalk(Guid id)
    {
        var talk = await db.TechTalkLogs.FindAsync(id);
        if (talk == null) return NotFound();
        db.TechTalkLogs.Remove(talk);
        await db.SaveChangesAsync();
        return Ok();
    }
}
