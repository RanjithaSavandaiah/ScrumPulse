namespace ScrumPulse.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Domain.Common;
using ScrumPulse.Domain.Entities;

/// <summary>
/// EF Core DbContext with global soft-delete query filters, automatic audit stamping,
/// and optimistic concurrency support.
/// </summary>
public class AppDbContext : DbContext, IAppDbContext
{
    private readonly ITenantContext? _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext? tenantContext = null) : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Team> Teams => Set<Team>();
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

        // ── Global Soft-Delete Query Filter ──────────────────────────────
        // All entities inheriting BaseEntity automatically filter out IsDeleted=true records.
        // Use IgnoreQueryFilters() when you need to include soft-deleted records.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                var property = System.Linq.Expressions.Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
                var falseConstant = System.Linq.Expressions.Expression.Constant(false);
                var lambda = System.Linq.Expressions.Expression.Lambda(
                    System.Linq.Expressions.Expression.Equal(property, falseConstant),
                    parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }

        // ── Concurrency Token Configuration ──────────────────────────────
        // SQLite does not natively support [Timestamp]/rowversion, so we skip
        // concurrency token configuration when using SQLite provider.
        // For PostgreSQL, the RowVersion is auto-configured via [Timestamp] attribute.
    }

    /// <summary>
    /// Overrides SaveChangesAsync to automatically stamp audit fields (CreatedAtUtc, UpdatedAtUtc)
    /// on all entities inheriting from BaseEntity.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditFields();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampAuditFields();
        return base.SaveChanges();
    }

    private void StampAuditFields()
    {
        var now = DateTime.UtcNow;
        var currentUser = !string.IsNullOrWhiteSpace(_tenantContext?.CurrentUser)
            ? _tenantContext.CurrentUser
            : "Scrum Master";

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // Only stamp if CreatedAtUtc hasn't been explicitly set
                    // (within 5 seconds of now = auto-generated default)
                    if (Math.Abs((entry.Entity.CreatedAtUtc - now).TotalSeconds) < 5)
                    {
                        entry.Entity.CreatedAtUtc = now;
                    }
                    entry.Entity.IsDeleted = false;
                    if (string.IsNullOrWhiteSpace(entry.Entity.CreatedBy))
                    {
                        entry.Entity.CreatedBy = currentUser;
                    }
                    if (string.IsNullOrWhiteSpace(entry.Entity.UpdatedBy))
                    {
                        entry.Entity.UpdatedBy = currentUser;
                    }
                    if (entry.Entity.TeamId == null && _tenantContext?.CurrentTeamId != null)
                    {
                        entry.Entity.TeamId = _tenantContext.CurrentTeamId;
                    }
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = now;
                    entry.Entity.UpdatedBy = currentUser;
                    // Prevent changing CreatedAtUtc and CreatedBy on update
                    entry.Property(nameof(BaseEntity.CreatedAtUtc)).IsModified = false;
                    entry.Property(nameof(BaseEntity.CreatedBy)).IsModified = false;
                    break;
            }
        }
    }
}
