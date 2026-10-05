namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using ScrumPulse.Api.Filters;
using ScrumPulse.Application.CQRS;
using ScrumPulse.Application.CQRS.Teams;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;

/// <summary>
/// Multi team tenant management controller enabling squad onboarding,
/// discovery, and context switching across an enterprise.
/// </summary>
public class TeamsController : BaseApiController
{
    private readonly IMediator _mediator;
    private readonly IIdempotencyStore? _idempotencyStore;

    [ActivatorUtilitiesConstructor]
    public TeamsController(IMediator mediator, IIdempotencyStore? idempotencyStore = null)
    {
        _mediator = mediator;
        _idempotencyStore = idempotencyStore;
    }

    /// <summary>Testing constructor providing backward compatibility for direct DbContext tests.</summary>
    public TeamsController(IAppDbContext db, IIdempotencyStore? idempotencyStore = null)
        : this(CreateMediatorForTesting(db), idempotencyStore)
    {
    }

    [HttpGet]
    [OutputCache(PolicyName = "ShortLived")]
    [ProducesResponseType(typeof(IEnumerable<TeamDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamDto>>> GetAll(CancellationToken ct = default) =>
        Ok(await _mediator.QueryAsync(new GetTeamsQuery(), ct));

    [HttpGet("{id:guid}")]
    [OutputCache(PolicyName = "ShortLived")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var team = await _mediator.QueryAsync(new GetTeamByIdQuery(id), ct);
        return team != null ? Ok(team) : NotFound();
    }

    [HttpPost]
    [RequireScrumMaster]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TeamDto>> Create(
        [FromBody] CreateTeamRequest request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey) && _idempotencyStore != null)
        {
            var cached = await _idempotencyStore.GetResponseAsync<TeamDto>(idempotencyKey, ct);
            if (cached != null) return Ok(cached);
        }

        var result = await _mediator.SendAsync(new CreateTeamCommand(request), ct);
        if (result.IsFailure)
        {
            if (result.ErrorCode == "CONFLICT")
                return Conflict(new { error = result.Error });

            return BadRequest(new { error = result.Error });
        }

        var dto = result.Value!;
        if (!string.IsNullOrWhiteSpace(idempotencyKey) && _idempotencyStore != null)
        {
            await _idempotencyStore.SaveResponseAsync(idempotencyKey, dto, null, ct);
        }

        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPost("join")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> Join([FromBody] JoinTeamRequest request, CancellationToken ct = default)
    {
        var result = await _mediator.SendAsync(new JoinTeamCommand(request), ct);
        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> Update(Guid id, [FromBody] CreateTeamRequest request, CancellationToken ct = default)
    {
        var team = await _mediator.SendAsync(new UpdateTeamCommand(id, request), ct);
        return team != null ? Ok(team) : NotFound();
    }

    [HttpPut("{id:guid}/quality-gates")]
    [RequireScrumMaster]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> ConfigureQualityGates(
        Guid id,
        [FromBody] ConfigureTeamGatesRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.SendAsync(new ConfigureQualityGatesCommand(id, request), ct);
        if (result.IsFailure)
        {
            return result.ErrorCode switch
            {
                "BAD_REQUEST" => BadRequest(new { error = result.Error }),
                "NOT_FOUND" => NotFound(),
                _ => BadRequest(new { error = result.Error })
            };
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}/quality-gates")]
    [ProducesResponseType(typeof(ConfigureTeamGatesRequest), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConfigureTeamGatesRequest>> GetQualityGates(Guid id, CancellationToken ct = default)
    {
        var gates = await _mediator.QueryAsync(new GetQualityGatesQuery(id), ct);
        return gates != null ? Ok(gates) : NotFound();
    }

    private static IMediator CreateMediatorForTesting(IAppDbContext db)
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton<IQueryHandler<GetTeamsQuery, IEnumerable<TeamDto>>>(new GetTeamsQueryHandler(db));
        services.AddSingleton<IQueryHandler<GetTeamByIdQuery, TeamDto?>>(new GetTeamByIdQueryHandler(db));
        services.AddSingleton<IQueryHandler<GetQualityGatesQuery, ConfigureTeamGatesRequest?>>(new GetQualityGatesQueryHandler(db));
        services.AddSingleton<ICommandHandler<CreateTeamCommand, Domain.Common.Result<TeamDto>>>(new CreateTeamCommandHandler(db));
        services.AddSingleton<ICommandHandler<JoinTeamCommand, Domain.Common.Result<TeamDto>>>(new JoinTeamCommandHandler(db));
        services.AddSingleton<ICommandHandler<UpdateTeamCommand, TeamDto?>>(new UpdateTeamCommandHandler(db));
        services.AddSingleton<ICommandHandler<ConfigureQualityGatesCommand, Domain.Common.Result<TeamDto>>>(new ConfigureQualityGatesCommandHandler(db));
        return new ScrumPulse.Infrastructure.Services.AppMediator(services.BuildServiceProvider());
    }
}
