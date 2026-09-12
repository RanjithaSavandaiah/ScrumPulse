namespace ScrumPulse.Application.CQRS.Leaves;

using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Mapping;
using ScrumPulse.Domain.Common;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;

// ── Queries ──────────────────────────────────────────────────────────────

public record GetLeavesQuery(
    Guid? MemberId = null,
    int? Year = null,
    int? Month = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null
) : IQuery<IEnumerable<TeamLeaveDto>>;

public class GetLeavesQueryHandler(IAppDbContext db) : IQueryHandler<GetLeavesQuery, IEnumerable<TeamLeaveDto>>
{
    private const int MinValidCalendarYear = 2000;
    private const int MinCalendarMonth = 1;
    private const int MaxCalendarMonth = 12;

    public async Task<IEnumerable<TeamLeaveDto>> HandleAsync(GetLeavesQuery query, CancellationToken ct = default)
    {
        var dbQuery = db.TeamLeaves
            .IgnoreQueryFilters()
            .Include(leave => leave.TeamMember)
            .Where(teamLeave => teamLeave.IsDeleted != true)
            .AsQueryable();

        if (query.MemberId.HasValue)
            dbQuery = dbQuery.Where(teamLeave => teamLeave.TeamMemberId == query.MemberId.Value);

        if (query.StartDate.HasValue && query.EndDate.HasValue)
        {
            var windowStartUtc = DateTime.SpecifyKind(query.StartDate.Value.Date, DateTimeKind.Utc);
            var windowEndUtc = DateTime.SpecifyKind(query.EndDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            dbQuery = dbQuery.Where(teamLeave => teamLeave.StartDate <= windowEndUtc && teamLeave.EndDate >= windowStartUtc);
        }
        else if (query.Year.HasValue && query.Month.HasValue && query.Year.Value >= MinValidCalendarYear && query.Month.Value >= MinCalendarMonth && query.Month.Value <= MaxCalendarMonth)
        {
            var startOfMonth = new DateTime(query.Year.Value, query.Month.Value, 1, 0, 0, 0, DateTimeKind.Utc);
            var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);
            dbQuery = dbQuery.Where(teamLeave => teamLeave.StartDate <= endOfMonth && teamLeave.EndDate >= startOfMonth);
        }
        else if (query.Year.HasValue && query.Year.Value >= MinValidCalendarYear)
        {
            var startOfYear = new DateTime(query.Year.Value, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var endOfYear = new DateTime(query.Year.Value, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc);
            dbQuery = dbQuery.Where(teamLeave => teamLeave.StartDate <= endOfYear && teamLeave.EndDate >= startOfYear);
        }

        var list = await dbQuery
            .OrderByDescending(leave => leave.StartDate)
            .AsNoTracking()
            .ToListAsync(ct);

        return list.ToDtos();
    }
}

public record GetLeaveByIdQuery(Guid Id) : IQuery<TeamLeaveDto?>;

public class GetLeaveByIdQueryHandler(IAppDbContext db) : IQueryHandler<GetLeaveByIdQuery, TeamLeaveDto?>
{
    public async Task<TeamLeaveDto?> HandleAsync(GetLeaveByIdQuery query, CancellationToken ct = default)
    {
        var leave = await db.TeamLeaves
            .Include(l => l.TeamMember)
            .FirstOrDefaultAsync(l => l.Id == query.Id, ct);

        return leave?.ToDto();
    }
}

// ── Commands ─────────────────────────────────────────────────────────────

public record SubmitLeaveCommand(SubmitLeaveRequest Request, string? CurrentUser = null) : ICommand<Result<TeamLeaveDto>>;

public class SubmitLeaveCommandHandler(IAppDbContext db) : ICommandHandler<SubmitLeaveCommand, Result<TeamLeaveDto>>
{
    public async Task<Result<TeamLeaveDto>> HandleAsync(SubmitLeaveCommand command, CancellationToken ct = default)
    {
        var request = command.Request;
        if (request.TeamMemberId == Guid.Empty)
        {
            return Result<TeamLeaveDto>.Failure("Please select a squad member", "BAD_REQUEST");
        }

        var startDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc);
        var rawEnd = request.EndDate < request.StartDate ? request.StartDate : request.EndDate;
        var endDate = DateTime.SpecifyKind(rawEnd, DateTimeKind.Utc);

        var creator = !string.IsNullOrWhiteSpace(request.CreatedBy)
            ? request.CreatedBy.Trim()
            : (!string.IsNullOrWhiteSpace(command.CurrentUser) ? command.CurrentUser : "Developer");

        var leave = new TeamLeave
        {
            TeamMemberId = request.TeamMemberId,
            StartDate = startDate,
            EndDate = endDate,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Planned Leave" : request.Reason.Trim(),
            LeaveType = ParseLeaveCategory(request.LeaveType),
            LeaveSlot = Enum.TryParse<LeaveSlotType>(request.LeaveSlot, true, out var slot) ? slot : LeaveSlotType.FullDay,
            Location = string.IsNullOrWhiteSpace(request.Location) ? "Offshore" : request.Location.Trim(),
            IsApproved = true,
            CreatedBy = creator,
            UpdatedBy = creator
        };

        db.TeamLeaves.Add(leave);
        await db.SaveChangesAsync(ct);

        var member = await db.TeamMembers.FirstOrDefaultAsync(teamMember => teamMember.Id == request.TeamMemberId, ct);
        leave.TeamMember = member;

        return Result<TeamLeaveDto>.Success(leave.ToDto());
    }

    public static LeaveCategory ParseLeaveCategory(string? input) => input?.Trim() switch
    {
        "Sick Leave" or "SickLeave" => LeaveCategory.SickLeave,
        "Comp Off" or "CompensatoryOff" => LeaveCategory.CompensatoryOff,
        "Offshore Public Holiday" or "PublicHoliday" => LeaveCategory.PublicHoliday,
        _ => LeaveCategory.PrivilegeLeave
    };
}

public record UpdateLeaveCommand(Guid Id, SubmitLeaveRequest Request, string? CurrentUser = null) : ICommand<Result<TeamLeaveDto>>;

public class UpdateLeaveCommandHandler(IAppDbContext db) : ICommandHandler<UpdateLeaveCommand, Result<TeamLeaveDto>>
{
    public async Task<Result<TeamLeaveDto>> HandleAsync(UpdateLeaveCommand command, CancellationToken ct = default)
    {
        var leave = await db.TeamLeaves.FindAsync([command.Id], ct);
        if (leave == null) return Result<TeamLeaveDto>.Failure("Leave not found", "NOT_FOUND");

        var request = command.Request;
        var startDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc);
        var rawEnd = request.EndDate < request.StartDate ? request.StartDate : request.EndDate;
        var endDate = DateTime.SpecifyKind(rawEnd, DateTimeKind.Utc);

        var updater = !string.IsNullOrWhiteSpace(request.CreatedBy)
            ? request.CreatedBy.Trim()
            : (!string.IsNullOrWhiteSpace(command.CurrentUser) ? command.CurrentUser : "Developer");

        leave.TeamMemberId = request.TeamMemberId;
        leave.StartDate = startDate;
        leave.EndDate = endDate;
        leave.Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Planned Leave" : request.Reason.Trim();
        leave.LeaveType = SubmitLeaveCommandHandler.ParseLeaveCategory(request.LeaveType);
        leave.LeaveSlot = Enum.TryParse<LeaveSlotType>(request.LeaveSlot, true, out var slot) ? slot : LeaveSlotType.FullDay;
        if (!string.IsNullOrWhiteSpace(request.Location)) leave.Location = request.Location.Trim();
        leave.UpdatedBy = updater;
        if (string.IsNullOrWhiteSpace(leave.CreatedBy))
        {
            leave.CreatedBy = updater;
        }

        await db.SaveChangesAsync(ct);

        var member = await db.TeamMembers.FirstOrDefaultAsync(existingMember => existingMember.Id == request.TeamMemberId, ct);
        leave.TeamMember = member;

        return Result<TeamLeaveDto>.Success(leave.ToDto());
    }
}

public record DeleteLeaveCommand(Guid Id) : ICommand<bool>;

public class DeleteLeaveCommandHandler(IAppDbContext db) : ICommandHandler<DeleteLeaveCommand, bool>
{
    public async Task<bool> HandleAsync(DeleteLeaveCommand command, CancellationToken ct = default)
    {
        var leave = await db.TeamLeaves.FindAsync([command.Id], ct);
        if (leave == null) return false;

        db.TeamLeaves.Remove(leave);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
