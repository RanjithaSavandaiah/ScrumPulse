namespace ScrumPulse.Application.CQRS.Standups;

using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Mapping;
using ScrumPulse.Domain.Common;
using ScrumPulse.Domain.Entities;

// ── Queries ──────────────────────────────────────────────────────────────

public record GetStandupsQuery(Guid? SprintId = null, Guid? MemberId = null, DateTime? Date = null)
    : IQuery<IEnumerable<DailyStandupDto>>;

public class GetStandupsQueryHandler(IAppDbContext db) : IQueryHandler<GetStandupsQuery, IEnumerable<DailyStandupDto>>
{
    public async Task<IEnumerable<DailyStandupDto>> HandleAsync(GetStandupsQuery query, CancellationToken ct = default)
    {
        var dbQuery = db.DailyStandups
            .Include(standup => standup.TeamMember)
            .AsQueryable();

        if (query.SprintId.HasValue) dbQuery = dbQuery.Where(standup => standup.SprintId == query.SprintId.Value);
        if (query.MemberId.HasValue) dbQuery = dbQuery.Where(standup => standup.TeamMemberId == query.MemberId.Value);
        if (query.Date.HasValue)
        {
            var targetDate = query.Date.Value.Date;
            dbQuery = dbQuery.Where(standup => standup.StandupDate.Date == targetDate);
        }

        var list = await dbQuery
            .OrderByDescending(standup => standup.StandupDate)
            .ThenByDescending(standup => standup.CreatedAtUtc)
            .AsNoTracking()
            .ToListAsync(ct);

        return list.ToDtos();
    }
}

public record GetStandupByIdQuery(Guid Id) : IQuery<DailyStandupDto?>;

public class GetStandupByIdQueryHandler(IAppDbContext db) : IQueryHandler<GetStandupByIdQuery, DailyStandupDto?>
{
    public async Task<DailyStandupDto?> HandleAsync(GetStandupByIdQuery query, CancellationToken ct = default)
    {
        var standup = await db.DailyStandups
            .Include(s => s.TeamMember)
            .FirstOrDefaultAsync(s => s.Id == query.Id, ct);

        return standup?.ToDto();
    }
}

// ── Commands ─────────────────────────────────────────────────────────────

public record SubmitStandupCommand(SubmitStandupRequest Request) : ICommand<Result<DailyStandupDto>>;

public class SubmitStandupCommandHandler(IAppDbContext db) : ICommandHandler<SubmitStandupCommand, Result<DailyStandupDto>>
{
    public async Task<Result<DailyStandupDto>> HandleAsync(SubmitStandupCommand command, CancellationToken ct = default)
    {
        var request = command.Request;
        if (request.TeamMemberId == Guid.Empty)
        {
            return Result<DailyStandupDto>.Failure("Please select a team member", "BAD_REQUEST");
        }

        if (string.IsNullOrWhiteSpace(request.YesterdaySummary))
        {
            return Result<DailyStandupDto>.Failure("Yesterday summary is mandatory", "BAD_REQUEST");
        }

        if (string.IsNullOrWhiteSpace(request.TodayPlan))
        {
            return Result<DailyStandupDto>.Failure("Today plan is mandatory", "BAD_REQUEST");
        }

        var standup = new DailyStandup
        {
            TeamMemberId = request.TeamMemberId,
            SprintId = request.SprintId,
            YesterdaySummary = request.YesterdaySummary,
            TodayPlan = request.TodayPlan,
            BlockersText = request.BlockersText ?? "None",
            MoodScore = request.MoodScore,
            StandupDate = DateTime.UtcNow
        };

        db.DailyStandups.Add(standup);
        await db.SaveChangesAsync(ct);

        var member = await db.TeamMembers.FirstOrDefaultAsync(teamMember => teamMember.Id == request.TeamMemberId, ct);
        standup.TeamMember = member;

        return Result<DailyStandupDto>.Success(standup.ToDto());
    }
}

public record UpdateStandupCommand(Guid Id, SubmitStandupRequest Request) : ICommand<Result<DailyStandupDto>>;

public class UpdateStandupCommandHandler(IAppDbContext db) : ICommandHandler<UpdateStandupCommand, Result<DailyStandupDto>>
{
    public async Task<Result<DailyStandupDto>> HandleAsync(UpdateStandupCommand command, CancellationToken ct = default)
    {
        var request = command.Request;
        if (request.TeamMemberId == Guid.Empty)
        {
            return Result<DailyStandupDto>.Failure("Please select a team member", "BAD_REQUEST");
        }

        if (string.IsNullOrWhiteSpace(request.YesterdaySummary))
        {
            return Result<DailyStandupDto>.Failure("Yesterday summary is mandatory", "BAD_REQUEST");
        }

        if (string.IsNullOrWhiteSpace(request.TodayPlan))
        {
            return Result<DailyStandupDto>.Failure("Today plan is mandatory", "BAD_REQUEST");
        }

        var standup = await db.DailyStandups.FindAsync([command.Id], ct);
        if (standup == null) return Result<DailyStandupDto>.Failure("Standup not found", "NOT_FOUND");

        standup.TeamMemberId = request.TeamMemberId;
        if (request.SprintId.HasValue) standup.SprintId = request.SprintId;
        standup.YesterdaySummary = request.YesterdaySummary;
        standup.TodayPlan = request.TodayPlan;
        standup.BlockersText = request.BlockersText ?? "None";
        standup.MoodScore = request.MoodScore;

        await db.SaveChangesAsync(ct);

        var member = await db.TeamMembers.FirstOrDefaultAsync(existingMember => existingMember.Id == request.TeamMemberId, ct);
        standup.TeamMember = member;

        return Result<DailyStandupDto>.Success(standup.ToDto());
    }
}

public record DeleteStandupCommand(Guid Id) : ICommand<bool>;

public class DeleteStandupCommandHandler(IAppDbContext db) : ICommandHandler<DeleteStandupCommand, bool>
{
    public async Task<bool> HandleAsync(DeleteStandupCommand command, CancellationToken ct = default)
    {
        var item = await db.DailyStandups.FindAsync([command.Id], ct);
        if (item == null) return false;

        db.DailyStandups.Remove(item);
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public record ClearAllStandupsCommand : ICommand<int>;

public class ClearAllStandupsCommandHandler(IAppDbContext db) : ICommandHandler<ClearAllStandupsCommand, int>
{
    public async Task<int> HandleAsync(ClearAllStandupsCommand command, CancellationToken ct = default)
    {
        return await db.DailyStandups.ExecuteDeleteAsync(ct);
    }
}
