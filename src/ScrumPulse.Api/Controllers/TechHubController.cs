namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Mapping;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;

/// <summary>Tech hub controller for tech debt and tech talks with typed DTOs.</summary>
public class TechHubController(IAppDbContext db) : BaseApiController
{
    // ── Tech Debt ────────────────────────────────────────────────────────

    [HttpGet("debt")]
    [HttpGet("tech-debt")]
    [ProducesResponseType(typeof(IEnumerable<TechDebtItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TechDebtItemDto>>> GetTechDebt(CancellationToken ct)
    {
        var list = await db.TechDebtItems
            .Include(t => t.Assignee)
            .OrderByDescending(t => t.CreatedAtUtc)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(list.ToDtos());
    }

    [HttpPost("debt")]
    [HttpPost("tech-debt")]
    [ProducesResponseType(typeof(TechDebtItemDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TechDebtItemDto>> CreateTechDebt([FromBody] CreateTechDebtRequest request, CancellationToken ct)
    {
        var item = new TechDebtItem
        {
            Title = request.Title,
            Description = request.Description,
            Severity = request.Severity,
            EstimatedHours = request.EstimatedHours,
            Status = TechDebtStatus.Identified,
            PayoffSprintId = request.PayoffSprintId,
            AssigneeId = request.AssigneeId
        };

        db.TechDebtItems.Add(item);
        await db.SaveChangesAsync(ct);

        if (item.AssigneeId.HasValue)
        {
            item.Assignee = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == item.AssigneeId.Value, ct);
        }

        return Ok(item.ToDto());
    }

    [HttpPost("debt/{id:guid}/resolve")]
    [HttpPost("tech-debt/{id:guid}/resolve")]
    [ProducesResponseType(typeof(TechDebtItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TechDebtItemDto>> ResolveTechDebt(Guid id, [FromBody] ResolveTechDebtRequest? request, CancellationToken ct)
    {
        var item = await db.TechDebtItems.Include(t => t.Assignee).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (item == null) return NotFound();
        item.Status = request?.Status ?? TechDebtStatus.Resolved;
        await db.SaveChangesAsync(ct);

        return Ok(item.ToDto());
    }

    [HttpPut("debt/{id:guid}")]
    [HttpPut("tech-debt/{id:guid}")]
    [ProducesResponseType(typeof(TechDebtItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TechDebtItemDto>> UpdateTechDebt(Guid id, [FromBody] UpdateTechDebtRequest request, CancellationToken ct)
    {
        var item = await db.TechDebtItems.Include(t => t.Assignee).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (item == null) return NotFound();

        item.Title = request.Title;
        item.Description = request.Description;
        item.Severity = request.Severity;
        item.EstimatedHours = request.EstimatedHours;
        item.Status = request.Status;
        item.PayoffSprintId = request.PayoffSprintId;
        item.AssigneeId = request.AssigneeId;
        await db.SaveChangesAsync(ct);

        if (item.AssigneeId.HasValue && item.Assignee == null)
        {
            item.Assignee = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == item.AssigneeId.Value, ct);
        }

        return Ok(item.ToDto());
    }

    [HttpDelete("debt/{id:guid}")]
    [HttpDelete("tech-debt/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTechDebt(Guid id, CancellationToken ct)
    {
        var item = await db.TechDebtItems.FindAsync([id], ct);
        if (item == null) return NotFound();
        db.TechDebtItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Tech Talks ───────────────────────────────────────────────────────

    [HttpGet("talks")]
    [HttpGet("tech-talks")]
    [ProducesResponseType(typeof(IEnumerable<TechTalkLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TechTalkLogDto>>> GetTechTalks(CancellationToken ct)
    {
        var list = await db.TechTalkLogs
            .Include(talk => talk.Presenter)
            .OrderByDescending(talk => talk.TalkDate)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(list.ToDtos());
    }

    [HttpPost("talks")]
    [HttpPost("tech-talks")]
    [ProducesResponseType(typeof(TechTalkLogDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TechTalkLogDto>> CreateTechTalk([FromBody] CreateTechTalkRequest request, CancellationToken ct)
    {
        var log = new TechTalkLog
        {
            Topic = request.Topic,
            PresenterId = request.PresenterId,
            TalkDate = request.TalkDate ?? DateTime.UtcNow,
            DurationMinutes = request.DurationMinutes,
            KeyTakeaways = request.KeyTakeaways,
            SlidesUrl = request.SlidesUrl
        };

        db.TechTalkLogs.Add(log);
        await db.SaveChangesAsync(ct);
        var presenter = await db.TeamMembers.FindAsync([log.PresenterId], ct);
        log.Presenter = presenter;

        return Ok(log.ToDto());
    }

    [HttpPut("talks/{id:guid}")]
    [HttpPut("tech-talks/{id:guid}")]
    [ProducesResponseType(typeof(TechTalkLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TechTalkLogDto>> UpdateTechTalk(Guid id, [FromBody] UpdateTechTalkRequest request, CancellationToken ct)
    {
        var talk = await db.TechTalkLogs.FindAsync([id], ct);
        if (talk == null) return NotFound();
        talk.Topic = request.Topic;
        talk.PresenterId = request.PresenterId;
        talk.TalkDate = request.TalkDate;
        talk.DurationMinutes = request.DurationMinutes;
        talk.KeyTakeaways = request.KeyTakeaways;
        talk.SlidesUrl = request.SlidesUrl;
        await db.SaveChangesAsync(ct);

        var presenter = await db.TeamMembers.FindAsync([talk.PresenterId], ct);
        talk.Presenter = presenter;
        return Ok(talk.ToDto());
    }

    [HttpDelete("talks/{id:guid}")]
    [HttpDelete("tech-talks/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTechTalk(Guid id, CancellationToken ct)
    {
        var talk = await db.TechTalkLogs.FindAsync([id], ct);
        if (talk == null) return NotFound();
        db.TechTalkLogs.Remove(talk);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
