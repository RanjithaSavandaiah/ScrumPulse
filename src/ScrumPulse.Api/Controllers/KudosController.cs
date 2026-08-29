namespace ScrumPulse.Api.Controllers;

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Mapping;
using ScrumPulse.Domain.Entities;

/// <summary>Kudos wall for team recognition and reactions.</summary>
public class KudosController(IAppDbContext db) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<KudosDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<KudosDto>>> GetAll(CancellationToken ct)
    {
        var list = await db.KudosCards
            .Include(kudos => kudos.Sender)
            .Include(kudos => kudos.Receiver)
            .OrderByDescending(kudos => kudos.CreatedAtUtc)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(list.ToDtos());
    }

    [HttpPost]
    [ProducesResponseType(typeof(KudosDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<KudosDto>> Send([FromBody] SendKudosRequest request, CancellationToken ct)
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
        await db.SaveChangesAsync(ct);

        kudos.Sender = await db.TeamMembers.FirstOrDefaultAsync(member => member.Id == request.SenderId, ct);
        kudos.Receiver = await db.TeamMembers.FirstOrDefaultAsync(member => member.Id == request.ReceiverId, ct);

        return Ok(kudos.ToDto());
    }

    [HttpPost("{id:guid}/reaction")]
    [HttpPost("{id:guid}/react")]
    [ProducesResponseType(typeof(KudosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddReaction(Guid id, [FromQuery] string? emoji, [FromBody] ReactToKudosRequest? bodyRequest = null, CancellationToken ct = default)
    {
        var reactionKey = !string.IsNullOrWhiteSpace(emoji)
            ? emoji
            : (bodyRequest?.ReactionType ?? bodyRequest?.Emoji ?? "🚀");

        var kudos = await db.KudosCards
            .Include(k => k.Sender)
            .Include(k => k.Receiver)
            .FirstOrDefaultAsync(kudosEntity => kudosEntity.Id == id, ct);
        if (kudos == null) return NotFound();

        var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(kudos.ReactionEmojisJson) ?? [];
        dict[reactionKey] = dict.GetValueOrDefault(reactionKey, 0) + 1;
        kudos.ReactionEmojisJson = JsonSerializer.Serialize(dict);

        await db.SaveChangesAsync(ct);

        return Ok(kudos.ToDto());
    }
}
