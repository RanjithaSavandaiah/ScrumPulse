namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using ScrumPulse.Application.CQRS;
using ScrumPulse.Application.CQRS.Blockers;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;

public class BlockersController(
    IMediator mediator,
    IIdempotencyStore idempotencyStore
) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BlockerDto>>> GetAll([FromQuery] Guid? sprintId, CancellationToken ct = default) =>
        Ok(await mediator.QueryAsync(new GetBlockersQuery(sprintId), ct));

    [HttpPost]
    [ProducesResponseType(typeof(BlockerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BlockerDto>> Create(
        [FromBody] CreateBlockerRequest request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { message = "Blocker title is mandatory" });
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest(new { message = "Blocker context is mandatory" });
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var cached = await idempotencyStore.GetResponseAsync<BlockerDto>(idempotencyKey);
            if (cached != null) return Ok(cached);
        }

        var result = await mediator.SendAsync(new CreateBlockerCommand(request), ct);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await idempotencyStore.SaveResponseAsync(idempotencyKey, result);
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/resolve")]
    [ProducesResponseType(typeof(BlockerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BlockerDto>> Resolve(Guid id, [FromBody] ResolveBlockerRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ResolutionNotes))
        {
            return BadRequest(new { message = "Resolution notes are mandatory" });
        }

        var result = await mediator.SendAsync(new ResolveBlockerCommand(id, request), ct);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BlockerDto>> Update(Guid id, [FromBody] CreateBlockerRequest request, CancellationToken ct = default)
    {
        var result = await mediator.SendAsync(new UpdateBlockerCommand(id, request), ct);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var success = await mediator.SendAsync(new DeleteBlockerCommand(id), ct);
        if (!success) return NotFound();
        return NoContent();
    }
}
