namespace ScrumPulse.Api.Controllers;

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Entities;

public class KudosController(IAppDbContext db) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<KudosDto>>> GetAll()
    {
        var list = await db.KudosCards
            .Include(kudos => kudos.Sender)
            .Include(kudos => kudos.Receiver)
            .OrderByDescending(kudos => kudos.CreatedAtUtc)
            .ToListAsync();

        return Ok(list.Select(kudos => new KudosDto(
            kudos.Id, kudos.SenderId, kudos.Sender?.Name ?? "Teammate",
            kudos.ReceiverId, kudos.Receiver?.Name ?? "Teammate",
            kudos.Badge, kudos.Message,
            JsonSerializer.Deserialize<Dictionary<string, int>>(kudos.ReactionEmojisJson) ?? [],
            kudos.CreatedAtUtc
        )));
    }

    [HttpPost]
    public async Task<ActionResult<KudosDto>> Send([FromBody] SendKudosRequest request)
    {
        var kudos = new KudosCard
        {
            SenderId = request.SenderId,
            ReceiverId = request.ReceiverId,
            Badge = request.Badge,
            Message = request.Message,
            ReactionEmojisJson = "{}"
        };

        db.KudosCards.Add(kudos);
        await db.SaveChangesAsync();

        var sender = await db.TeamMembers.FirstOrDefaultAsync(member => member.Id == request.SenderId);
        var receiver = await db.TeamMembers.FirstOrDefaultAsync(member => member.Id == request.ReceiverId);

        return Ok(new KudosDto(
            kudos.Id, kudos.SenderId, sender?.Name ?? "Teammate",
            kudos.ReceiverId, receiver?.Name ?? "Teammate",
            kudos.Badge, kudos.Message, [],
            kudos.CreatedAtUtc
        ));
    }

    [HttpPost("{id:guid}/reaction")]
    [HttpPost("{id:guid}/react")]
    public async Task<IActionResult> AddReaction(Guid id, [FromQuery] string? emoji, [FromBody] ReactToKudosRequest? bodyRequest = null)
    {
        var reactionKey = !string.IsNullOrWhiteSpace(emoji)
            ? emoji
            : (bodyRequest?.ReactionType ?? bodyRequest?.Emoji ?? "🚀");

        var kudos = await db.KudosCards
            .Include(k => k.Sender)
            .Include(k => k.Receiver)
            .FirstOrDefaultAsync(kudosEntity => kudosEntity.Id == id);
        if (kudos == null) return NotFound();

        var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(kudos.ReactionEmojisJson) ?? [];
        dict[reactionKey] = dict.GetValueOrDefault(reactionKey, 0) + 1;
        kudos.ReactionEmojisJson = JsonSerializer.Serialize(dict);

        await db.SaveChangesAsync();

        return Ok(new KudosDto(
            kudos.Id, kudos.SenderId, kudos.Sender?.Name ?? "Teammate",
            kudos.ReceiverId, kudos.Receiver?.Name ?? "Teammate",
            kudos.Badge, kudos.Message, dict,
            kudos.CreatedAtUtc
        ));
    }
}
