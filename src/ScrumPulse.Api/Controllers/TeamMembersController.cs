namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Domain.Entities;

public class TeamMembersController(IAppDbContext db) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TeamMember>>> GetAll() =>
        Ok(await db.TeamMembers.Where(teamMember => teamMember.IsActive && !teamMember.IsDeleted).OrderBy(teamMember => teamMember.Name).ToListAsync());

    [HttpPost]
    public async Task<ActionResult<TeamMember>> Create([FromBody] TeamMember member)
    {
        if (string.IsNullOrWhiteSpace(member.Avatar))
        {
            var initials = string.Join("", member.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => s[0])).ToUpper();
            member.Avatar = initials.Length > 2 ? initials.Substring(0, 2) : initials;
        }
        member.IsActive = true;
        member.IsDeleted = false;
        db.TeamMembers.Add(member);
        await db.SaveChangesAsync();
        return Ok(member);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TeamMember>> Update(Guid id, [FromBody] TeamMember updated)
    {
        var member = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == id);
        if (member == null) return NotFound();

        member.Name = updated.Name;
        member.Email = updated.Email;
        member.Role = updated.Role;
        member.Location = updated.Location;
        member.TimeZone = updated.TimeZone;
        member.ActiveWipLimit = updated.ActiveWipLimit;
        if (!string.IsNullOrWhiteSpace(updated.Avatar)) member.Avatar = updated.Avatar;

        await db.SaveChangesAsync();
        return Ok(member);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var member = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == id);
        if (member == null) return NotFound();

        member.IsActive = false;
        member.IsDeleted = true;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
