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
/// Multi-team tenant management controller enabling squad onboarding,
/// discovery, and context switching across an enterprise.
/// </summary>
public class TeamsController(IAppDbContext db) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TeamDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamDto>>> GetAll(CancellationToken ct = default)
    {
        var teams = await db.Teams
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(teams.ToDtos());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == id, ct);
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
        var existingSlug = await db.Teams.AnyAsync(t => t.Slug == slug, ct);
        if (existingSlug)
        {
            slug = $"{slug}-{RandomNumberGenerator.GetInt32(100, 999)}";
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
        var team = await db.Teams.FirstOrDefaultAsync(t => t.JoinCode == code && t.IsActive, ct);
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
        var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == id, ct);
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
        return string.IsNullOrWhiteSpace(slug) ? $"team-{RandomNumberGenerator.GetInt32(1000, 9999)}" : slug;
    }

    private static string GenerateJoinCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(6);
        var result = new char[6];
        for (int i = 0; i < 6; i++)
        {
            result[i] = chars[bytes[i] % chars.Length];
        }
        return new string(result);
    }
}
