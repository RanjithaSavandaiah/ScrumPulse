namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Entities;

/// <summary>Team member management with request DTOs to prevent mass assignment.</summary>
public class TeamMembersController(IAppDbContext db) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TeamMember>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamMember>>> GetAll(CancellationToken ct) =>
        Ok(await db.TeamMembers
            .Where(teamMember => teamMember.IsActive && !teamMember.IsDeleted)
            .OrderBy(teamMember => teamMember.Name)
            .AsNoTracking()
            .ToListAsync(ct));

    [HttpPost]
    [ProducesResponseType(typeof(TeamMember), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamMember>> Create([FromBody] CreateTeamMemberRequest request, CancellationToken ct)
    {
        var avatar = request.Avatar;
        if (string.IsNullOrWhiteSpace(avatar))
        {
            var initials = string.Join("", request.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => s[0])).ToUpper();
            avatar = initials.Length > 2 ? initials[..2] : initials;
        }

        var member = new TeamMember
        {
            Name = request.Name,
            Email = request.Email,
            Role = request.Role,
            Location = request.Location,
            TimeZone = request.TimeZone,
            Avatar = avatar,
            ActiveWipLimit = request.ActiveWipLimit,
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
    public async Task<ActionResult<TeamMember>> Update(Guid id, [FromBody] UpdateTeamMemberRequest request, CancellationToken ct)
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

        await db.SaveChangesAsync(ct);
        return Ok(member);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var member = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (member == null) return NotFound();

        member.IsActive = false;
        member.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
