namespace ScrumPulse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Domain.Entities;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<Blocker> Blockers => Set<Blocker>();
    public DbSet<DailyStandup> DailyStandups => Set<DailyStandup>();
    public DbSet<TeamLeave> TeamLeaves => Set<TeamLeave>();
    public DbSet<Monthly1on1Feedback> Monthly1on1Feedbacks => Set<Monthly1on1Feedback>();
    public DbSet<RetroCard> RetroCards => Set<RetroCard>();
    public DbSet<RetroActionItem> RetroActionItems => Set<RetroActionItem>();
    public DbSet<KudosCard> KudosCards => Set<KudosCard>();
    public DbSet<TechDebtItem> TechDebtItems => Set<TechDebtItem>();
    public DbSet<TechTalkLog> TechTalkLogs => Set<TechTalkLog>();
    public DbSet<PullRequestReviewLog> PullRequestReviewLogs => Set<PullRequestReviewLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
