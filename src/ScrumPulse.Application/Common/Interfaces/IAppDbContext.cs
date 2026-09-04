namespace ScrumPulse.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Domain.Entities;

public interface IAppDbContext
{
    DbSet<Team> Teams { get; }
    DbSet<Sprint> Sprints { get; }
    DbSet<TeamMember> TeamMembers { get; }
    DbSet<WorkItem> WorkItems { get; }
    DbSet<Blocker> Blockers { get; }
    DbSet<DailyStandup> DailyStandups { get; }
    DbSet<TeamLeave> TeamLeaves { get; }
    DbSet<Monthly1on1Feedback> Monthly1on1Feedbacks { get; }
    DbSet<RetroCard> RetroCards { get; }
    DbSet<RetroActionItem> RetroActionItems { get; }
    DbSet<KudosCard> KudosCards { get; }
    DbSet<TechDebtItem> TechDebtItems { get; }
    DbSet<TechTalkLog> TechTalkLogs { get; }
    DbSet<PullRequestReviewLog> PullRequestReviewLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
