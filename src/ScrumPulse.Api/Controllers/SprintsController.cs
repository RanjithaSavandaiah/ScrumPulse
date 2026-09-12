namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using ScrumPulse.Application.CQRS;
using ScrumPulse.Application.CQRS.Sprints;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;

/// <summary>Sprint management — thin controller delegating to CQRS handlers.</summary>
public class SprintsController(IMediator mediator, ITenantContext? tenantContext = null) : BaseApiController
{
    [HttpGet]
    [OutputCache(PolicyName = "ShortLived")]
    [ProducesResponseType(typeof(IEnumerable<SprintDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SprintDto>>> GetAll(CancellationToken ct) =>
        Ok(await mediator.QueryAsync(new GetSprintsQuery(), ct));

    [HttpPost]
    [ProducesResponseType(typeof(SprintDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SprintDto>> Create([FromBody] CreateSprintRequest request, CancellationToken ct)
    {
        if (request.EndDate < request.StartDate)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Sprint Date Range",
                Detail = "Sprint EndDate must be on or after StartDate."
            });
        }

        return Ok(await mediator.SendAsync(new CreateSprintCommand(request, tenantContext?.CurrentTeamId), ct));
    }

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(typeof(SprintDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateSprint(Guid id, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new ActivateSprintCommand(id), ct);
        return result != null ? Ok(result) : NotFound();
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SprintDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SprintDto>> Update(Guid id, [FromBody] UpdateSprintRequest request, CancellationToken ct)
    {
        if (request.EndDate < request.StartDate)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Sprint Date Range",
                Detail = "Sprint EndDate must be on or after StartDate."
            });
        }

        var result = await mediator.SendAsync(new UpdateSprintCommand(id, request), ct);
        return result != null ? Ok(result) : NotFound();
    }

    [HttpPost("{id:guid}/confidence")]
    [ProducesResponseType(typeof(SprintDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateConfidence(Guid id, [FromQuery] int score, [FromQuery] string? notes, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new UpdateConfidenceCommand(id, score, notes), ct);
        return result != null ? Ok(result) : NotFound();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var success = await mediator.SendAsync(new DeleteSprintCommand(id), ct);
        return success ? NoContent() : NotFound();
    }
}
