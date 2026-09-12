namespace ScrumPulse.Application.CQRS.Sprints;

using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Entities;

// ── Queries ──────────────────────────────────────────────────────────────

public record GetSprintsQuery : IQuery<IEnumerable<SprintDto>>;

public class GetSprintsQueryHandler(IAppDbContext db) : IQueryHandler<GetSprintsQuery, IEnumerable<SprintDto>>
{
    public async Task<IEnumerable<SprintDto>> HandleAsync(GetSprintsQuery query, CancellationToken ct = default)
    {
        var sprints = await db.Sprints
            .AsNoTracking()
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(ct);

        return sprints.Select(ToDto);
    }

    internal static SprintDto ToDto(Sprint s) => new(
        s.Id, s.Name, s.Goal, s.StartDate, s.EndDate, s.IsActive,
        s.CommittedStoryPoints, s.DeliveredStoryPoints, s.ConfidenceScore,
        s.ConfidenceNotes, s.DailyWorkingHours, s.TeamId);
}

// ── Commands ─────────────────────────────────────────────────────────────

public record CreateSprintCommand(CreateSprintRequest Request, Guid? TeamId) : ICommand<SprintDto>;

public class CreateSprintCommandHandler(IAppDbContext db) : ICommandHandler<CreateSprintCommand, SprintDto>
{
    public async Task<SprintDto> HandleAsync(CreateSprintCommand command, CancellationToken ct = default)
    {
        var request = command.Request;

        var sprint = new Sprint
        {
            Name = request.Name,
            Goal = request.Goal,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = request.IsActive,
            CommittedStoryPoints = request.CommittedStoryPoints,
            ConfidenceScore = request.ConfidenceScore,
            ConfidenceNotes = request.ConfidenceNotes,
            DailyWorkingHours = request.DailyWorkingHours,
            TeamId = command.TeamId
        };

        if (sprint.IsActive)
        {
            var activeList = await db.Sprints.Where(s => s.IsActive).ToListAsync(ct);
            foreach (var s in activeList) s.IsActive = false;
        }

        db.Sprints.Add(sprint);
        await db.SaveChangesAsync(ct);
        return GetSprintsQueryHandler.ToDto(sprint);
    }
}

public record UpdateSprintCommand(Guid SprintId, UpdateSprintRequest Request) : ICommand<SprintDto?>;

public class UpdateSprintCommandHandler(IAppDbContext db) : ICommandHandler<UpdateSprintCommand, SprintDto?>
{
    public async Task<SprintDto?> HandleAsync(UpdateSprintCommand command, CancellationToken ct = default)
    {
        var sprint = await db.Sprints.FirstOrDefaultAsync(s => s.Id == command.SprintId, ct);
        if (sprint == null) return null;

        var r = command.Request;
        sprint.Name = r.Name;
        sprint.Goal = r.Goal;
        sprint.StartDate = r.StartDate;
        sprint.EndDate = r.EndDate;
        sprint.CommittedStoryPoints = r.CommittedStoryPoints;
        sprint.DeliveredStoryPoints = r.DeliveredStoryPoints;
        if (r.ConfidenceScore > 0) sprint.ConfidenceScore = r.ConfidenceScore;
        if (r.ConfidenceNotes != null) sprint.ConfidenceNotes = r.ConfidenceNotes;
        if (r.DailyWorkingHours > 0) sprint.DailyWorkingHours = r.DailyWorkingHours;

        await db.SaveChangesAsync(ct);
        return GetSprintsQueryHandler.ToDto(sprint);
    }
}

public record DeleteSprintCommand(Guid SprintId) : ICommand<bool>;

public class DeleteSprintCommandHandler(IAppDbContext db) : ICommandHandler<DeleteSprintCommand, bool>
{
    public async Task<bool> HandleAsync(DeleteSprintCommand command, CancellationToken ct = default)
    {
        var sprint = await db.Sprints.FirstOrDefaultAsync(s => s.Id == command.SprintId, ct);
        if (sprint == null) return false;

        // Unlink dependent entities before deletion
        await UnlinkDependentsAsync(db, command.SprintId, ct);

        bool wasActive = sprint.IsActive;
        db.Sprints.Remove(sprint);

        if (wasActive)
        {
            var next = await db.Sprints
                .Where(s => s.Id != command.SprintId)
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync(ct);
            if (next != null) next.IsActive = true;
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    private static async Task UnlinkDependentsAsync(IAppDbContext db, Guid sprintId, CancellationToken ct)
    {
        var workItems = await db.WorkItems.Where(w => w.SprintId == sprintId).ToListAsync(ct);
        foreach (var w in workItems) w.SprintId = null;

        var standups = await db.DailyStandups.Where(s => s.SprintId == sprintId).ToListAsync(ct);
        foreach (var s in standups) s.SprintId = null;

        var blockers = await db.Blockers.Where(b => b.SprintId == sprintId).ToListAsync(ct);
        foreach (var b in blockers) b.SprintId = null;

        var retros = await db.RetroCards.Where(r => r.SprintId == sprintId).ToListAsync(ct);
        foreach (var r in retros) r.SprintId = null;

        var actions = await db.RetroActionItems.Where(a => a.SprintId == sprintId).ToListAsync(ct);
        foreach (var a in actions) a.SprintId = null;

        var prs = await db.PullRequestReviewLogs.Where(p => p.SprintId == sprintId).ToListAsync(ct);
        foreach (var p in prs) p.SprintId = null;

        var debts = await db.TechDebtItems.Where(t => t.PayoffSprintId == sprintId).ToListAsync(ct);
        foreach (var d in debts) d.PayoffSprintId = null;
    }
}

public record ActivateSprintCommand(Guid SprintId) : ICommand<SprintDto?>;

public class ActivateSprintCommandHandler(IAppDbContext db) : ICommandHandler<ActivateSprintCommand, SprintDto?>
{
    public async Task<SprintDto?> HandleAsync(ActivateSprintCommand command, CancellationToken ct = default)
    {
        var target = await db.Sprints.FirstOrDefaultAsync(s => s.Id == command.SprintId, ct);
        if (target == null) return null;

        var others = await db.Sprints.Where(s => s.IsActive && s.Id != command.SprintId).ToListAsync(ct);
        foreach (var s in others) s.IsActive = false;

        target.IsActive = true;
        await db.SaveChangesAsync(ct);
        return GetSprintsQueryHandler.ToDto(target);
    }
}

public record UpdateConfidenceCommand(Guid SprintId, int Score, string? Notes) : ICommand<SprintDto?>;

public class UpdateConfidenceCommandHandler(IAppDbContext db) : ICommandHandler<UpdateConfidenceCommand, SprintDto?>
{
    public async Task<SprintDto?> HandleAsync(UpdateConfidenceCommand command, CancellationToken ct = default)
    {
        var sprint = await db.Sprints.FirstOrDefaultAsync(s => s.Id == command.SprintId, ct);
        if (sprint == null) return null;

        sprint.ConfidenceScore = Math.Clamp(command.Score, 1, 10);
        sprint.ConfidenceNotes = command.Notes;
        await db.SaveChangesAsync(ct);
        return GetSprintsQueryHandler.ToDto(sprint);
    }
}
