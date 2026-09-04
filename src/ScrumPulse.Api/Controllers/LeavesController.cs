namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Mapping;
using ScrumPulse.Application.Services;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;

/// <summary>Leave management with capacity calculation integration.</summary>
public class LeavesController(IAppDbContext db, IMetricsCalculatorService metricsCalculatorService) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TeamLeaveDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamLeaveDto>>> GetAll(
        [FromQuery] Guid? memberId,
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken ct = default)
    {
        var query = db.TeamLeaves
            .Include(leave => leave.TeamMember)
            .AsQueryable();

        if (memberId.HasValue) query = query.Where(l => l.TeamMemberId == memberId.Value);
        if (year.HasValue && month.HasValue)
        {
            var startOfMonth = new DateTime(year.Value, month.Value, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
            query = query.Where(l => l.StartDate <= endOfMonth && l.EndDate >= startOfMonth);
        }

        var list = await query
            .OrderByDescending(leave => leave.StartDate)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(list.ToDtos());
    }

    [HttpPost]
    [ProducesResponseType(typeof(TeamLeaveDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamLeaveDto>> Submit([FromBody] SubmitLeaveRequest request, CancellationToken ct = default)
    {
        var startDate = request.StartDate;
        var endDate = request.EndDate < request.StartDate ? request.StartDate : request.EndDate;

        var leave = new TeamLeave
        {
            TeamMemberId = request.TeamMemberId,
            StartDate = startDate,
            EndDate = endDate,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Planned Leave" : request.Reason.Trim(),
            LeaveType = Enum.TryParse<LeaveCategory>(request.LeaveType, true, out var parsed) ? parsed : LeaveCategory.PrivilegeLeave,
            LeaveSlot = Enum.TryParse<LeaveSlotType>(request.LeaveSlot, true, out var slot) ? slot : LeaveSlotType.FullDay,
            Location = string.IsNullOrWhiteSpace(request.Location) ? "Offshore" : request.Location.Trim(),
            IsApproved = true
        };
        db.TeamLeaves.Add(leave);
        await db.SaveChangesAsync(ct);

        var member = await db.TeamMembers.FirstOrDefaultAsync(teamMember => teamMember.Id == request.TeamMemberId, ct);
        leave.TeamMember = member;

        return Ok(leave.ToDto());
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TeamLeaveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamLeaveDto>> Update(Guid id, [FromBody] SubmitLeaveRequest request, CancellationToken ct = default)
    {
        var leave = await db.TeamLeaves.FindAsync([id], ct);
        if (leave == null) return NotFound();

        var startDate = request.StartDate;
        var endDate = request.EndDate < request.StartDate ? request.StartDate : request.EndDate;

        leave.TeamMemberId = request.TeamMemberId;
        leave.StartDate = startDate;
        leave.EndDate = endDate;
        leave.Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Planned Leave" : request.Reason.Trim();
        leave.LeaveType = Enum.TryParse<LeaveCategory>(request.LeaveType, true, out var parsed) ? parsed : LeaveCategory.PrivilegeLeave;
        leave.LeaveSlot = Enum.TryParse<LeaveSlotType>(request.LeaveSlot, true, out var slot) ? slot : LeaveSlotType.FullDay;
        if (!string.IsNullOrWhiteSpace(request.Location)) leave.Location = request.Location.Trim();

        await db.SaveChangesAsync(ct);

        var member = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == request.TeamMemberId, ct);
        leave.TeamMember = member;

        return Ok(leave.ToDto());
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var leave = await db.TeamLeaves.FindAsync([id], ct);
        if (leave == null) return NotFound();
        db.TeamLeaves.Remove(leave);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("capacity/{sprintId:guid}")]
    [HttpGet("sprint/{sprintId:guid}/capacity")]
    [ProducesResponseType(typeof(SprintCapacityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SprintCapacityDto>> GetCapacity(Guid sprintId, CancellationToken ct = default) =>
        Ok(await metricsCalculatorService.CalculateSprintCapacityAsync(sprintId, ct));
}
