namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using ScrumPulse.Application.CQRS;
using ScrumPulse.Application.CQRS.Leaves;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Services;
using ScrumPulse.Domain.Enums;

/// <summary>Leave management with capacity calculation integration — thin controller delegating to CQRS.</summary>
public class LeavesController : BaseApiController
{
    private readonly IMediator _mediator;
    private readonly IMetricsCalculatorService _metricsCalculatorService;
    private readonly ITenantContext? _tenantContext;
    private readonly ILogger<LeavesController>? _logger;

    [ActivatorUtilitiesConstructor]
    public LeavesController(
        IMediator mediator,
        IMetricsCalculatorService metricsCalculatorService,
        ITenantContext? tenantContext = null,
        ILogger<LeavesController>? logger = null)
    {
        _mediator = mediator;
        _metricsCalculatorService = metricsCalculatorService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>Testing constructor providing backward compatibility for direct DbContext tests.</summary>
    public LeavesController(
        IAppDbContext db,
        IMetricsCalculatorService metricsCalculatorService,
        ITenantContext? tenantContext = null,
        ILogger<LeavesController>? logger = null)
        : this(CreateMediatorForTesting(db), metricsCalculatorService, tenantContext, logger)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TeamLeaveDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamLeaveDto>>> GetAll(
        [FromQuery] Guid? memberId = null,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        CancellationToken ct = default,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var leaves = await _mediator.QueryAsync(
                new GetLeavesQuery(memberId, year, month, startDate, endDate), ct);
            return Ok(leaves);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load leaves: {Message}", ex.Message);
            return Ok(Array.Empty<TeamLeaveDto>());
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(TeamLeaveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TeamLeaveDto>> Submit([FromBody] SubmitLeaveRequest request, CancellationToken ct = default)
    {
        var result = await _mediator.SendAsync(new SubmitLeaveCommand(request, _tenantContext?.CurrentUser), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { message = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TeamLeaveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamLeaveDto>> Update(Guid id, [FromBody] SubmitLeaveRequest request, CancellationToken ct = default)
    {
        var result = await _mediator.SendAsync(new UpdateLeaveCommand(id, request, _tenantContext?.CurrentUser), ct);
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
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var deleted = await _mediator.SendAsync(new DeleteLeaveCommand(id), ct);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("capacity/{sprintId:guid}")]
    [HttpGet("sprint/{sprintId:guid}/capacity")]
    [ProducesResponseType(typeof(SprintCapacityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SprintCapacityDto>> GetCapacity(Guid sprintId, CancellationToken ct = default) =>
        Ok(await _metricsCalculatorService.CalculateSprintCapacityAsync(sprintId, ct));

    public static LeaveCategory ParseLeaveCategory(string? input) =>
        SubmitLeaveCommandHandler.ParseLeaveCategory(input);

    private static IMediator CreateMediatorForTesting(IAppDbContext db)
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton<IQueryHandler<GetLeavesQuery, IEnumerable<TeamLeaveDto>>>(new GetLeavesQueryHandler(db));
        services.AddSingleton<IQueryHandler<GetLeaveByIdQuery, TeamLeaveDto?>>(new GetLeaveByIdQueryHandler(db));
        services.AddSingleton<ICommandHandler<SubmitLeaveCommand, Domain.Common.Result<TeamLeaveDto>>>(new SubmitLeaveCommandHandler(db));
        services.AddSingleton<ICommandHandler<UpdateLeaveCommand, Domain.Common.Result<TeamLeaveDto>>>(new UpdateLeaveCommandHandler(db));
        services.AddSingleton<ICommandHandler<DeleteLeaveCommand, bool>>(new DeleteLeaveCommandHandler(db));
        return new ScrumPulse.Infrastructure.Services.AppMediator(services.BuildServiceProvider());
    }
}
