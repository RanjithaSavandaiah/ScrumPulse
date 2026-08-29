namespace ScrumPulse.Api.Controllers;

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Mapping;
using ScrumPulse.Domain.Entities;

/// <summary>Sprint retrospective cards and action items management.</summary>
public class RetrospectivesController(IAppDbContext db) : BaseApiController
{
    [HttpGet("cards")]
    [ProducesResponseType(typeof(IEnumerable<RetroCardDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<RetroCardDto>>> GetCards([FromQuery] Guid? sprintId, CancellationToken ct)
    {
        var query = db.RetroCards.Include(retroCard => retroCard.Author).AsQueryable();
        if (sprintId.HasValue) query = query.Where(retroCard => retroCard.SprintId == sprintId.Value);

        var list = await query.OrderByDescending(retroCard => retroCard.UpvotesCount).AsNoTracking().ToListAsync(ct);
        return Ok(list.ToDtos());
    }

    [HttpPost("cards")]
    [ProducesResponseType(typeof(RetroCardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RetroCardDto>> CreateCard([FromBody] CreateRetroCardRequest request, CancellationToken ct)
    {
        var retroCard = new RetroCard
        {
            SprintId = request.SprintId,
            Category = request.Category,
            Content = request.Content,
            AuthorId = request.AuthorId,
            IsAnonymous = request.IsAnonymous,
            UpvotesCount = 1
        };
        db.RetroCards.Add(retroCard);
        await db.SaveChangesAsync(ct);

        var author = await db.TeamMembers.FirstOrDefaultAsync(member => member.Id == request.AuthorId, ct);
        retroCard.Author = author;

        return Ok(retroCard.ToDto());
    }

    [HttpPost("cards/{id:guid}/vote")]
    [ProducesResponseType(typeof(RetroCardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VoteCard(Guid id, CancellationToken ct)
    {
        var card = await db.RetroCards.Include(c => c.Author).FirstOrDefaultAsync(retroCard => retroCard.Id == id, ct);
        if (card == null) return NotFound();
        card.UpvotesCount += 1;
        await db.SaveChangesAsync(ct);

        return Ok(card.ToDto());
    }

    [HttpGet("actions")]
    [ProducesResponseType(typeof(IEnumerable<RetroActionItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<RetroActionItemDto>>> GetActionItems([FromQuery] Guid? sprintId, CancellationToken ct)
    {
        var query = db.RetroActionItems.Include(actionItem => actionItem.Assignee).AsQueryable();
        if (sprintId.HasValue) query = query.Where(actionItem => actionItem.SprintId == sprintId.Value);

        var list = await query.OrderBy(actionItem => actionItem.IsCompleted).ThenBy(actionItem => actionItem.DueDate)
            .AsNoTracking().ToListAsync(ct);
        return Ok(list.ToDtos());
    }

    [HttpPost("actions")]
    [ProducesResponseType(typeof(RetroActionItemDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RetroActionItemDto>> CreateActionItem([FromBody] CreateRetroActionItemRequest request, CancellationToken ct)
    {
        var actionItem = new RetroActionItem
        {
            SprintId = request.SprintId,
            Title = request.Title,
            AssigneeId = request.AssigneeId,
            DueDate = request.DueDate,
            IsCompleted = false
        };
        db.RetroActionItems.Add(actionItem);
        await db.SaveChangesAsync(ct);

        var assignee = await db.TeamMembers.FirstOrDefaultAsync(member => member.Id == request.AssigneeId, ct);
        actionItem.Assignee = assignee;

        return Ok(actionItem.ToDto());
    }

    [HttpPut("cards/{id:guid}")]
    [ProducesResponseType(typeof(RetroCardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RetroCardDto>> UpdateCard(Guid id, [FromBody] UpdateRetroCardRequest request, CancellationToken ct)
    {
        var card = await db.RetroCards.Include(c => c.Author).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (card == null) return NotFound();

        card.Category = request.Category;
        card.Content = request.Content;
        card.SprintId = request.SprintId;
        card.AuthorId = request.AuthorId;
        card.IsAnonymous = request.IsAnonymous;

        await db.SaveChangesAsync(ct);

        if (card.Author == null || card.AuthorId != request.AuthorId)
        {
            card.Author = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == request.AuthorId, ct);
        }

        return Ok(card.ToDto());
    }

    [HttpDelete("cards/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCard(Guid id, CancellationToken ct)
    {
        var card = await db.RetroCards.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (card == null) return NotFound();
        db.RetroCards.Remove(card);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("actions/{id:guid}")]
    [ProducesResponseType(typeof(RetroActionItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RetroActionItemDto>> UpdateActionItem(Guid id, [FromBody] UpdateRetroActionItemRequest request, CancellationToken ct)
    {
        var item = await db.RetroActionItems.Include(a => a.Assignee).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (item == null) return NotFound();

        item.Title = request.Title;
        item.SprintId = request.SprintId;
        item.AssigneeId = request.AssigneeId;
        item.DueDate = request.DueDate;
        item.IsCompleted = request.IsCompleted;

        await db.SaveChangesAsync(ct);

        if (item.Assignee == null || item.AssigneeId != request.AssigneeId)
        {
            item.Assignee = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == request.AssigneeId, ct);
        }

        return Ok(item.ToDto());
    }

    [HttpDelete("actions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteActionItem(Guid id, CancellationToken ct)
    {
        var item = await db.RetroActionItems.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (item == null) return NotFound();
        db.RetroActionItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("actions/{id:guid}/toggle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleActionItem(Guid id, CancellationToken ct)
    {
        var actionItem = await db.RetroActionItems.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (actionItem == null) return NotFound();
        actionItem.IsCompleted = !actionItem.IsCompleted;
        await db.SaveChangesAsync(ct);
        return Ok(new { actionItem.IsCompleted });
    }
}
