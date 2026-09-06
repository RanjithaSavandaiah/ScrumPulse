namespace ScrumPulse.Api.Controllers;

using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Mapping;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;

/// <summary>
/// Multi team tenant management controller enabling squad onboarding,
/// discovery, and context switching across an enterprise.
/// </summary>
public class TeamsController(IAppDbContext db) : BaseApiController
{
    private const int MinSlugRandomSuffix = 100;
    private const int MaxSlugRandomSuffix = 1000;
    private const int MinFallbackSlugRandom = 1000;
    private const int MaxFallbackSlugRandom = 10000;
    private const int JoinCodeLength = 6;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TeamDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamDto>>> GetAll(CancellationToken ct = default)
    {
        var teams = await db.Teams
            .Where(team => team.IsActive)
            .OrderBy(team => team.Name)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(teams.ToDtos());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var team = await db.Teams.FirstOrDefaultAsync(teamEntity => teamEntity.Id == id, ct);
        if (team == null) return NotFound();

        return Ok(team.ToDto());
    }

    [HttpPost]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TeamDto>> Create([FromBody] CreateTeamRequest request, CancellationToken ct = default)
    {
        if (Request?.Headers != null && Request.Headers.TryGetValue("X-User-Role", out var roleHeader))
        {
            var rawRole = roleHeader.ToString().Replace(" ", "");
            if (Enum.TryParse<RoleType>(rawRole, ignoreCase: true, out var role) &&
                role != RoleType.ScrumMaster && role != RoleType.Cdl && role != RoleType.AgileCoach)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "Only Scrum Masters can create a new squad." });
            }
        }

        var slug = GenerateSlug(string.IsNullOrWhiteSpace(request.Slug) ? request.Name : request.Slug);
        var existingSlug = await db.Teams.AnyAsync(team => team.Slug == slug, ct);
        if (existingSlug)
        {
            slug = $"{slug}-{RandomNumberGenerator.GetInt32(MinSlugRandomSuffix, MaxSlugRandomSuffix)}";
        }

        var joinCode = GenerateJoinCode();

        var team = new Team
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Description = request.Description?.Trim() ?? string.Empty,
            JoinCode = joinCode,
            IsActive = true
        };

        db.Teams.Add(team);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = team.Id }, team.ToDto());
    }

    [HttpPost("join")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> Join([FromBody] JoinTeamRequest request, CancellationToken ct = default)
    {
        var code = request.JoinCode.Trim().ToUpperInvariant();
        var team = await db.Teams.FirstOrDefaultAsync(teamEntity => teamEntity.JoinCode == code && teamEntity.IsActive, ct);
        if (team == null)
        {
            return NotFound(new { error = "No active team found with the specified join code." });
        }

        return Ok(team.ToDto());
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> Update(Guid id, [FromBody] CreateTeamRequest request, CancellationToken ct = default)
    {
        var team = await db.Teams.FirstOrDefaultAsync(teamEntity => teamEntity.Id == id, ct);
        if (team == null) return NotFound();

        team.Name = request.Name.Trim();
        if (!string.IsNullOrWhiteSpace(request.Description)) team.Description = request.Description.Trim();
        await db.SaveChangesAsync(ct);

        return Ok(team.ToDto());
    }

    private static string GenerateSlug(string input)
    {
        var slug = input.ToLowerInvariant().Trim();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? $"team-{RandomNumberGenerator.GetInt32(MinFallbackSlugRandom, MaxFallbackSlugRandom)}" : slug;
    }

    private static string GenerateJoinCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(JoinCodeLength);
        var result = new char[JoinCodeLength];
        for (int characterIndex = 0; characterIndex < JoinCodeLength; characterIndex++)
        {
            result[characterIndex] = chars[bytes[characterIndex] % chars.Length];
        }
        return new string(result);
    }
}
