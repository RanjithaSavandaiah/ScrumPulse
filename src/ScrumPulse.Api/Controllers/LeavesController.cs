namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Services;
using ScrumPulse.Domain.Entities;

public class LeavesController(IAppDbContext db, IMetricsCalculatorService metricsCalculatorService) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TeamLeaveDto>>> GetAll(
        [FromQuery] Guid? memberId,
        [FromQuery] int? year,
        [FromQuery] int? month)
    {
        var query = db.TeamLeaves
            .Include(leave => leave.TeamMember)
            .AsQueryable();

        if (memberId.HasValue)
        {
            query = query.Where(l => l.TeamMemberId == memberId.Value);
        }

        if (year.HasValue && month.HasValue)
        {
            var startOfMonth = new DateTime(year.Value, month.Value, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
            query = query.Where(l => l.StartDate <= endOfMonth && l.EndDate >= startOfMonth);
        }

        var list = await query
            .OrderByDescending(leave => leave.StartDate)
            .ToListAsync();

        return Ok(list.Select(leave => new TeamLeaveDto(
            leave.Id,
            leave.TeamMemberId,
            leave.TeamMember?.Name ?? "Member",
            leave.StartDate,
            leave.EndDate,
            leave.Reason,
            leave.LeaveType,
            leave.Location,
            leave.IsApproved,
            leave.TotalDays,
            leave.LeaveSlot
        )));
    }

    [HttpPost]
    public async Task<ActionResult<TeamLeaveDto>> Submit([FromBody] SubmitLeaveRequest request)
    {
        var startDate = request.StartDate;
        var endDate = request.EndDate < request.StartDate ? request.StartDate : request.EndDate;

        var leave = new TeamLeave
        {
            TeamMemberId = request.TeamMemberId,
            StartDate = startDate,
            EndDate = endDate,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Planned Leave" : request.Reason.Trim(),
            LeaveType = string.IsNullOrWhiteSpace(request.LeaveType) ? "Privilege Leave" : request.LeaveType.Trim(),
            LeaveSlot = string.IsNullOrWhiteSpace(request.LeaveSlot) ? "FullDay" : request.LeaveSlot.Trim(),
            Location = string.IsNullOrWhiteSpace(request.Location) ? "Offshore" : request.Location.Trim(),
            IsApproved = true
        };
        db.TeamLeaves.Add(leave);
        await db.SaveChangesAsync();

        var member = await db.TeamMembers.FirstOrDefaultAsync(teamMember => teamMember.Id == request.TeamMemberId);

        return Ok(new TeamLeaveDto(
            leave.Id,
            leave.TeamMemberId,
            member?.Name ?? "Member",
            leave.StartDate,
            leave.EndDate,
            leave.Reason,
            leave.LeaveType,
            leave.Location,
            leave.IsApproved,
            leave.TotalDays,
            leave.LeaveSlot
        ));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TeamLeaveDto>> Update(Guid id, [FromBody] SubmitLeaveRequest request)
    {
        var leave = await db.TeamLeaves.FindAsync(id);
        if (leave == null) return NotFound();

        var startDate = request.StartDate;
        var endDate = request.EndDate < request.StartDate ? request.StartDate : request.EndDate;

        leave.TeamMemberId = request.TeamMemberId;
        leave.StartDate = startDate;
        leave.EndDate = endDate;
        leave.Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Planned Leave" : request.Reason.Trim();
        leave.LeaveType = string.IsNullOrWhiteSpace(request.LeaveType) ? "Privilege Leave" : request.LeaveType.Trim();
        leave.LeaveSlot = string.IsNullOrWhiteSpace(request.LeaveSlot) ? "FullDay" : request.LeaveSlot.Trim();
        if (!string.IsNullOrWhiteSpace(request.Location)) leave.Location = request.Location.Trim();

        await db.SaveChangesAsync();

        var member = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == request.TeamMemberId);

        return Ok(new TeamLeaveDto(
            leave.Id,
            leave.TeamMemberId,
            member?.Name ?? "Member",
            leave.StartDate,
            leave.EndDate,
            leave.Reason,
            leave.LeaveType,
            leave.Location,
            leave.IsApproved,
            leave.TotalDays,
            leave.LeaveSlot
        ));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var leave = await db.TeamLeaves.FindAsync(id);
        if (leave == null) return NotFound();

        db.TeamLeaves.Remove(leave);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("capacity/{sprintId:guid}")]
    [HttpGet("sprint/{sprintId:guid}/capacity")]
    public async Task<ActionResult<SprintCapacityDto>> GetCapacity(Guid sprintId) =>
        Ok(await metricsCalculatorService.CalculateSprintCapacityAsync(sprintId));
}
