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
    public async Task<ActionResult<IEnumerable<BlockerDto>>> GetAll([FromQuery] Guid? sprintId) =>
        Ok(await mediator.QueryAsync(new GetBlockersQuery(sprintId)));

    [HttpPost]
    public async Task<ActionResult<BlockerDto>> Create(
        [FromBody] CreateBlockerRequest request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var cached = await idempotencyStore.GetResponseAsync<BlockerDto>(idempotencyKey);
            if (cached != null) return Ok(cached);
        }

        var result = await mediator.SendAsync(new CreateBlockerCommand(request));

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await idempotencyStore.SaveResponseAsync(idempotencyKey, result);
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<ActionResult<BlockerDto>> Resolve(Guid id, [FromBody] ResolveBlockerRequest request)
    {
        var result = await mediator.SendAsync(new ResolveBlockerCommand(id, request));
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BlockerDto>> Update(Guid id, [FromBody] CreateBlockerRequest request)
    {
        var result = await mediator.SendAsync(new UpdateBlockerCommand(id, request));
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await mediator.SendAsync(new DeleteBlockerCommand(id));
        if (!success) return NotFound();
        return NoContent();
    }
}
