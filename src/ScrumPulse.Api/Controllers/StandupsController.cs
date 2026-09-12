namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using ScrumPulse.Application.CQRS;
using ScrumPulse.Application.CQRS.Standups;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;

/// <summary>Daily standup management with protected admin endpoints — thin controller delegating to CQRS.</summary>
public class StandupsController : BaseApiController
{
    private readonly IMediator _mediator;

    [ActivatorUtilitiesConstructor]
    public StandupsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Testing constructor providing backward compatibility for direct DbContext tests.</summary>
    public StandupsController(IAppDbContext db)
        : this(CreateMediatorForTesting(db))
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DailyStandupDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DailyStandupDto>>> GetAll(
        [FromQuery] Guid? sprintId = null,
        [FromQuery] Guid? memberId = null,
        [FromQuery] DateTime? date = null,
        CancellationToken ct = default) =>
        Ok(await _mediator.QueryAsync(new GetStandupsQuery(sprintId, memberId, date), ct));

    [HttpPost]
    [ProducesResponseType(typeof(DailyStandupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DailyStandupDto>> Submit([FromBody] SubmitStandupRequest request, CancellationToken ct)
    {
        var result = await _mediator.SendAsync(new SubmitStandupCommand(request), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { message = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DailyStandupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DailyStandupDto>> Update(Guid id, [FromBody] SubmitStandupRequest request, CancellationToken ct)
    {
        var result = await _mediator.SendAsync(new UpdateStandupCommand(id, request), ct);
        if (result.IsFailure)
        {
            return result.ErrorCode == "NOT_FOUND"
                ? NotFound()
                : BadRequest(new { message = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await _mediator.SendAsync(new DeleteStandupCommand(id), ct);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>
    /// Administrative endpoint to clear all standup data.
    /// Protected — requires the X-Admin-Key header matching the configured SM_PIN.
    /// </summary>
    [HttpDelete("clear-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ClearAll(
        [FromHeader(Name = "X-Admin-Key")] string? adminKey,
        [FromServices] IConfiguration configuration,
        CancellationToken ct)
    {
        var configuredPin = Environment.GetEnvironmentVariable("SM_PIN")
            ?? configuration["Auth:ScrumMasterPin"];

        if (string.IsNullOrWhiteSpace(adminKey) || adminKey != configuredPin)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Admin key required for bulk deletion." });
        }

        await _mediator.SendAsync(new ClearAllStandupsCommand(), ct);
        return NoContent();
    }

    private static IMediator CreateMediatorForTesting(IAppDbContext db)
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton<IQueryHandler<GetStandupsQuery, IEnumerable<DailyStandupDto>>>(new GetStandupsQueryHandler(db));
        services.AddSingleton<IQueryHandler<GetStandupByIdQuery, DailyStandupDto?>>(new GetStandupByIdQueryHandler(db));
        services.AddSingleton<ICommandHandler<SubmitStandupCommand, Domain.Common.Result<DailyStandupDto>>>(new SubmitStandupCommandHandler(db));
        services.AddSingleton<ICommandHandler<UpdateStandupCommand, Domain.Common.Result<DailyStandupDto>>>(new UpdateStandupCommandHandler(db));
        services.AddSingleton<ICommandHandler<DeleteStandupCommand, bool>>(new DeleteStandupCommandHandler(db));
        services.AddSingleton<ICommandHandler<ClearAllStandupsCommand, int>>(new ClearAllStandupsCommandHandler(db));
        return new ScrumPulse.Infrastructure.Services.AppMediator(services.BuildServiceProvider());
    }
}
