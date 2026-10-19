namespace ScrumPulse.Infrastructure.Persistence;

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Domain.Common;
using ScrumPulse.Domain.Entities;

/// <summary>
/// EF Core DbContext applying tenant and soft-delete query filters, automatic audit stamping,
/// and optimistic concurrency support.
/// </summary>
public class AppDbContext : DbContext, IAppDbContext
{
    private readonly ITenantContext? _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext? tenantContext = null) : base(options)
    {
        _tenantContext = tenantContext;
    }

    // Null in unit tests and background initialisers, where tenant filtering is bypassed.
    private bool HasTenantContext => _tenantContext != null;

    /// <summary>
    /// Empty rather than null is what makes the filter fail closed: no row is persisted with an
    /// empty TeamId, so an anonymous caller matches nothing, whereas a null comparison would
    /// return every row whose TeamId was never assigned.
    /// </summary>
    private Guid ActiveTeamId => _tenantContext?.CurrentTeamId ?? Guid.Empty;

    /// <summary>Audit identity for writes outside a request, such as seeding.</summary>
    private const string SystemAuditIdentity = "system";

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
    public DbSet<BoardConnection> BoardConnections => Set<BoardConnection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        ApplyGlobalQueryFilters(modelBuilder);

        // --- Concurrency Token Configuration --
        // SQLite does not natively support [Timestamp]/rowversion, so we skip
        // concurrency token configuration when using SQLite provider.
        // For PostgreSQL, the RowVersion is auto configured via [Timestamp] attribute.
    }

    /// <summary>
    /// Soft-delete filter on every entity, tenant filter on all but <see cref="Team"/>.
    /// </summary>
    /// <remarks>
    /// Tenancy lives here because it used to depend on every handler remembering a
    /// <c>Where</c> clause, and the ones that forgot exposed other squads' standups and
    /// one-to-one feedback. <see cref="Team"/> is exempt: a team row is keyed by its own
    /// <c>Id</c>, and joining a squad must find a team the caller is not yet scoped to, so
    /// team access is gated by join code and session token instead. Cross-team rollups opt out
    /// with <c>IgnoreQueryFilters()</c>.
    /// </remarks>
    private void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var entity = Expression.Parameter(entityType.ClrType, "entity");

            // entity.IsDeleted == false
            Expression filter = Expression.Equal(
                Expression.Property(entity, nameof(BaseEntity.IsDeleted)),
                Expression.Constant(false));

            if (entityType.ClrType != typeof(Team))
            {
                // !this.HasTenantContext || entity.TeamId == this.ActiveTeamId
                // Property access keeps it dynamic: EF re-evaluates per query rather than
                // baking in the first request's team.
                var hasTenantContext = Expression.Property(
                    Expression.Constant(this),
                    nameof(HasTenantContext));

                var notHasTenantContext = Expression.Not(hasTenantContext);

                var activeTeamId = Expression.Property(
                    Expression.Constant(this),
                    nameof(ActiveTeamId));

                var tenantMatches = Expression.Equal(
                    Expression.Property(entity, nameof(BaseEntity.TeamId)),
                    Expression.Convert(activeTeamId, typeof(Guid?)));

                var tenantFilter = Expression.OrElse(notHasTenantContext, tenantMatches);

                filter = Expression.AndAlso(filter, tenantFilter);
            }

            modelBuilder.Entity(entityType.ClrType)
                .HasQueryFilter(Expression.Lambda(filter, entity));
        }
    }

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
        var currentUser = string.IsNullOrWhiteSpace(_tenantContext?.CurrentUser)
            ? SystemAuditIdentity
            : _tenantContext.CurrentUser;
        var activeTeamId = _tenantContext?.CurrentTeamId;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    StampInsert(entry, now, currentUser, activeTeamId);
                    break;

                case EntityState.Modified:
                    StampUpdate(entry, now, currentUser);
                    break;
            }
        }
    }

    private static void StampInsert(
        EntityEntry<BaseEntity> entry,
        DateTime now,
        string currentUser,
        Guid? activeTeamId)
    {
        // Seeded and imported rows carry a historical timestamp the metrics depend on.
        if (entry.Entity.CreatedAtUtc == default)
        {
            entry.Entity.CreatedAtUtc = now;
        }

        entry.Entity.IsDeleted = false;

        if (string.IsNullOrWhiteSpace(entry.Entity.CreatedBy))
        {
            entry.Entity.CreatedBy = currentUser;
        }

        entry.Entity.UpdatedBy ??= entry.Entity.CreatedBy;

        // Ownership comes from the session, never the request body. Overwriting rather than
        // defaulting stops a caller planting a row in another squad via a posted TeamId.
        // Team rows are exempt: a team owns itself and its TeamId stays null.
        if (activeTeamId.HasValue && entry.Entity is not Team)
        {
            entry.Entity.TeamId = activeTeamId;
        }
    }

    private static void StampUpdate(EntityEntry<BaseEntity> entry, DateTime now, string currentUser)
    {
        entry.Entity.UpdatedAtUtc = now;
        entry.Entity.UpdatedBy = currentUser;

        // Creation metadata and ownership are immutable after insert. Marking them unmodified
        // blocks mass assignment: an update cannot rewrite history or move a row between
        // squads even if something upstream mutated the tracked entity. TeamMember is exempt
        // because members do get reassigned.
        entry.Property(nameof(BaseEntity.CreatedAtUtc)).IsModified = false;
        entry.Property(nameof(BaseEntity.CreatedBy)).IsModified = false;
        if (entry.Entity is not TeamMember)
        {
            entry.Property(nameof(BaseEntity.TeamId)).IsModified = false;
        }
    }
}
