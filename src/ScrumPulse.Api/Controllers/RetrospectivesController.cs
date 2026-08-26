namespace ScrumPulse.Api.Controllers;

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Entities;

public class RetrospectivesController(IAppDbContext db) : BaseApiController
{
    [HttpGet("cards")]
    public async Task<ActionResult<IEnumerable<RetroCardDto>>> GetCards([FromQuery] Guid? sprintId)
    {
        var query = db.RetroCards.Include(retroCard => retroCard.Author).AsQueryable();
        if (sprintId.HasValue) query = query.Where(retroCard => retroCard.SprintId == sprintId.Value);

        var list = await query.OrderByDescending(retroCard => retroCard.UpvotesCount).ToListAsync();
        return Ok(list.Select(retroCard => new RetroCardDto(
            retroCard.Id, retroCard.SprintId, retroCard.Category, retroCard.Content, retroCard.AuthorId,
            retroCard.IsAnonymous ? "Anonymous" : retroCard.Author?.Name,
            retroCard.IsAnonymous, retroCard.UpvotesCount,
            JsonSerializer.Deserialize<List<Guid>>(retroCard.UpvoterMemberIdsJson) ?? []
        )));
    }

    [HttpPost("cards")]
    public async Task<ActionResult<RetroCardDto>> CreateCard([FromBody] CreateRetroCardRequest request)
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
        await db.SaveChangesAsync();

        var author = await db.TeamMembers.FirstOrDefaultAsync(member => member.Id == request.AuthorId);

        return Ok(new RetroCardDto(
            retroCard.Id, retroCard.SprintId, retroCard.Category, retroCard.Content, retroCard.AuthorId,
            retroCard.IsAnonymous ? "Anonymous" : author?.Name,
            retroCard.IsAnonymous, retroCard.UpvotesCount, []
        ));
    }

    [HttpPost("cards/{id:guid}/vote")]
    public async Task<IActionResult> VoteCard(Guid id)
    {
        var card = await db.RetroCards.Include(c => c.Author).FirstOrDefaultAsync(retroCard => retroCard.Id == id);
        if (card == null) return NotFound();
        card.UpvotesCount += 1;
        await db.SaveChangesAsync();

        return Ok(new RetroCardDto(
            card.Id, card.SprintId, card.Category, card.Content, card.AuthorId,
            card.IsAnonymous ? "Anonymous" : card.Author?.Name,
            card.IsAnonymous, card.UpvotesCount,
            JsonSerializer.Deserialize<List<Guid>>(card.UpvoterMemberIdsJson) ?? []
        ));
    }

    [HttpGet("actions")]
    public async Task<ActionResult<IEnumerable<RetroActionItemDto>>> GetActionItems([FromQuery] Guid? sprintId)
    {
        var query = db.RetroActionItems.Include(actionItem => actionItem.Assignee).AsQueryable();
        if (sprintId.HasValue) query = query.Where(actionItem => actionItem.SprintId == sprintId.Value);

        var list = await query.OrderBy(actionItem => actionItem.IsCompleted).ThenBy(actionItem => actionItem.DueDate).ToListAsync();
        return Ok(list.Select(actionItem => new RetroActionItemDto(
            actionItem.Id, actionItem.SprintId, actionItem.Title, actionItem.AssigneeId, actionItem.Assignee?.Name, actionItem.DueDate, actionItem.IsCompleted
        )));
    }

    [HttpPost("actions")]
    public async Task<ActionResult<RetroActionItemDto>> CreateActionItem([FromBody] CreateRetroActionItemRequest request)
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
        await db.SaveChangesAsync();

        var assignee = await db.TeamMembers.FirstOrDefaultAsync(member => member.Id == request.AssigneeId);

        return Ok(new RetroActionItemDto(
            actionItem.Id, actionItem.SprintId, actionItem.Title, actionItem.AssigneeId, assignee?.Name, actionItem.DueDate, actionItem.IsCompleted
        ));
    }

    [HttpPut("cards/{id:guid}")]
    public async Task<ActionResult<RetroCardDto>> UpdateCard(Guid id, [FromBody] UpdateRetroCardRequest request)
    {
        var card = await db.RetroCards.Include(c => c.Author).FirstOrDefaultAsync(c => c.Id == id);
        if (card == null) return NotFound();

        card.Category = request.Category;
        card.Content = request.Content;
        card.SprintId = request.SprintId;
        card.AuthorId = request.AuthorId;
        card.IsAnonymous = request.IsAnonymous;

        await db.SaveChangesAsync();

        var author = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == request.AuthorId);

        return Ok(new RetroCardDto(
            card.Id, card.SprintId, card.Category, card.Content, card.AuthorId,
            card.IsAnonymous ? "Anonymous" : author?.Name,
            card.IsAnonymous, card.UpvotesCount,
            JsonSerializer.Deserialize<List<Guid>>(card.UpvoterMemberIdsJson) ?? []
        ));
    }

    [HttpDelete("cards/{id:guid}")]
    public async Task<IActionResult> DeleteCard(Guid id)
    {
        var card = await db.RetroCards.FirstOrDefaultAsync(c => c.Id == id);
        if (card == null) return NotFound();

        db.RetroCards.Remove(card);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("actions/{id:guid}")]
    public async Task<ActionResult<RetroActionItemDto>> UpdateActionItem(Guid id, [FromBody] UpdateRetroActionItemRequest request)
    {
        var item = await db.RetroActionItems.Include(a => a.Assignee).FirstOrDefaultAsync(a => a.Id == id);
        if (item == null) return NotFound();

        item.Title = request.Title;
        item.SprintId = request.SprintId;
        item.AssigneeId = request.AssigneeId;
        item.DueDate = request.DueDate;
        item.IsCompleted = request.IsCompleted;

        await db.SaveChangesAsync();

        var assignee = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == request.AssigneeId);

        return Ok(new RetroActionItemDto(
            item.Id, item.SprintId, item.Title, item.AssigneeId, assignee?.Name, item.DueDate, item.IsCompleted
        ));
    }

    [HttpDelete("actions/{id:guid}")]
    public async Task<IActionResult> DeleteActionItem(Guid id)
    {
        var item = await db.RetroActionItems.FirstOrDefaultAsync(a => a.Id == id);
        if (item == null) return NotFound();

        db.RetroActionItems.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("actions/{id:guid}/toggle")]
    public async Task<IActionResult> ToggleActionItem(Guid id)
    {
        var actionItem = await db.RetroActionItems.FirstOrDefaultAsync(item => item.Id == id);
        if (actionItem == null) return NotFound();
        actionItem.IsCompleted = !actionItem.IsCompleted;
        await db.SaveChangesAsync();
        return Ok(new { actionItem.IsCompleted });
    }
}
