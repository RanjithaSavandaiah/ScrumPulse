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
public class LeavesController(
    IAppDbContext db,
    IMetricsCalculatorService metricsCalculatorService,
    ILogger<LeavesController>? logger = null) : BaseApiController
{
    private const int MinValidCalendarYear = 2000;
    private const int MinCalendarMonth = 1;
    private const int MaxCalendarMonth = 12;

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
            var query = db.TeamLeaves
                .IgnoreQueryFilters()
                .Include(leave => leave.TeamMember)
                .Where(teamLeave => teamLeave.IsDeleted != true)
                .AsQueryable();

            if (memberId.HasValue) query = query.Where(teamLeave => teamLeave.TeamMemberId == memberId.Value);

            if (startDate.HasValue && endDate.HasValue)
            {
                var windowStartUtc = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
                var windowEndUtc = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                query = query.Where(teamLeave => teamLeave.StartDate <= windowEndUtc && teamLeave.EndDate >= windowStartUtc);
            }
            else if (year.HasValue && month.HasValue && year.Value >= MinValidCalendarYear && month.Value >= MinCalendarMonth && month.Value <= MaxCalendarMonth)
            {
                var startOfMonth = new DateTime(year.Value, month.Value, 1, 0, 0, 0, DateTimeKind.Utc);
                var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);
                query = query.Where(teamLeave => teamLeave.StartDate <= endOfMonth && teamLeave.EndDate >= startOfMonth);
            }
            else if (year.HasValue && year.Value >= MinValidCalendarYear)
            {
                // Calendar Year strictly follows Jan 1 to Dec 31
                var startOfYear = new DateTime(year.Value, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var endOfYear = new DateTime(year.Value, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc);
                query = query.Where(teamLeave => teamLeave.StartDate <= endOfYear && teamLeave.EndDate >= startOfYear);
            }

            var list = await query
                .OrderByDescending(leave => leave.StartDate)
                .AsNoTracking()
                .ToListAsync(ct);

            return Ok(list.ToDtos());
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to load leaves: {Message}", ex.Message);
            return Ok(Array.Empty<TeamLeaveDto>());
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(TeamLeaveDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamLeaveDto>> Submit([FromBody] SubmitLeaveRequest request, CancellationToken ct = default)
    {
        var startDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc);
        var rawEnd = request.EndDate < request.StartDate ? request.StartDate : request.EndDate;
        var endDate = DateTime.SpecifyKind(rawEnd, DateTimeKind.Utc);

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

        var startDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc);
        var rawEnd = request.EndDate < request.StartDate ? request.StartDate : request.EndDate;
        var endDate = DateTime.SpecifyKind(rawEnd, DateTimeKind.Utc);

        leave.TeamMemberId = request.TeamMemberId;
        leave.StartDate = startDate;
        leave.EndDate = endDate;
        leave.Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Planned Leave" : request.Reason.Trim();
        leave.LeaveType = Enum.TryParse<LeaveCategory>(request.LeaveType, true, out var parsed) ? parsed : LeaveCategory.PrivilegeLeave;
        leave.LeaveSlot = Enum.TryParse<LeaveSlotType>(request.LeaveSlot, true, out var slot) ? slot : LeaveSlotType.FullDay;
        if (!string.IsNullOrWhiteSpace(request.Location)) leave.Location = request.Location.Trim();

        await db.SaveChangesAsync(ct);

        var member = await db.TeamMembers.FirstOrDefaultAsync(existingMember => existingMember.Id == request.TeamMemberId, ct);
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
