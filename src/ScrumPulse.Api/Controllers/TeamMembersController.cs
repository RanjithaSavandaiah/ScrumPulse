namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Entities;

/// <summary>Team member management with request DTOs to prevent mass assignment.</summary>
public class TeamMembersController(IAppDbContext db, ITenantContext? tenantContext = null) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TeamMember>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamMember>>> GetAll(
        [FromQuery] Guid? teamId = null,
        [FromQuery] bool? all = null,
        CancellationToken ct = default)
    {
        var query = db.TeamMembers
            .Where(teamMember => teamMember.IsActive && !teamMember.IsDeleted);

        if (all != true)
        {
            var targetTeamId = teamId ?? tenantContext?.CurrentTeamId;
            if (targetTeamId.HasValue && targetTeamId.Value != Guid.Empty)
            {
                query = query.Where(teamMember => teamMember.TeamId == targetTeamId.Value);
            }
        }

        var members = await query
            .OrderBy(teamMember => teamMember.Name)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(members);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TeamMember), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamMember>> Create([FromBody] CreateTeamMemberRequest request, CancellationToken ct = default)
    {
        var avatar = request.Avatar;
        if (string.IsNullOrWhiteSpace(avatar))
        {
            var initials = string.Join("", request.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => s[0])).ToUpper();
            avatar = initials.Length > 2 ? initials[..2] : initials;
        }

        var assignedTeamId = request.TeamId ?? tenantContext?.CurrentTeamId;

        var member = new TeamMember
        {
            Name = request.Name,
            Email = request.Email,
            Role = request.Role,
            Location = request.Location,
            TimeZone = request.TimeZone,
            Avatar = avatar,
            ActiveWipLimit = request.ActiveWipLimit,
            TeamId = assignedTeamId,
            IsActive = true,
            IsDeleted = false
        };

        db.TeamMembers.Add(member);
        await db.SaveChangesAsync(ct);
        return Ok(member);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TeamMember), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamMember>> Update(Guid id, [FromBody] UpdateTeamMemberRequest request, CancellationToken ct = default)
    {
        var member = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (member == null) return NotFound();

        member.Name = request.Name;
        member.Email = request.Email;
        member.Role = request.Role;
        member.Location = request.Location;
        member.TimeZone = request.TimeZone;
        member.ActiveWipLimit = request.ActiveWipLimit;
        if (!string.IsNullOrWhiteSpace(request.Avatar)) member.Avatar = request.Avatar;
        if (request.TeamId.HasValue) member.TeamId = request.TeamId.Value;

        await db.SaveChangesAsync(ct);
        return Ok(member);
    }

    [HttpPut("{id:guid}/squad")]
    [ProducesResponseType(typeof(TeamMember), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamMember>> AssignSquad(Guid id, [FromBody] AssignMemberSquadRequest request, CancellationToken ct = default)
    {
        var member = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (member == null) return NotFound();

        member.TeamId = request.TeamId;
        await db.SaveChangesAsync(ct);
        return Ok(member);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var member = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (member == null) return NotFound();

        member.IsActive = false;
        member.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
