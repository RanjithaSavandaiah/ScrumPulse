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

    /// <summary>
    /// Whether a tenant context is active for this DbContext instance.
    /// When null (e.g. in unit tests or background initializers), tenant query filtering is bypassed.
    /// </summary>
    private bool HasTenantContext => _tenantContext != null;

    /// <summary>
    /// Team the current request is allowed to see, or <see cref="Guid.Empty"/> when there is no session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tenant query filter closes over this property, so EF re-evaluates it per query
    /// instead of baking the first request's team into the compiled model.
    /// </para>
    /// <para>
    /// Collapsing "no session" to <see cref="Guid.Empty"/> rather than null is what makes the
    /// filter fail closed. No row is ever persisted with an empty TeamId, so an anonymous
    /// caller matches nothing. Comparing against null would instead return every row whose
    /// TeamId was never assigned.
    /// </para>
    /// </remarks>
    private Guid ActiveTeamId => _tenantContext?.CurrentTeamId ?? Guid.Empty;

    /// <summary>
    /// Audit identity used for writes that happen outside a request, such as database seeding.
    /// </summary>
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
    /// Applies the soft-delete filter to every entity, and the tenant filter to every entity
    /// except <see cref="Team"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Enforcing tenancy here rather than in each query is deliberate. Previously isolation
    /// depended on every handler remembering to filter by team, and most did not, so any
    /// caller could read another squad's standups and one-to-one feedback. A model-level
    /// filter means a forgotten <c>Where</c> clause is no longer a data breach.
    /// </para>
    /// <para>
    /// <see cref="Team"/> is exempt because a team row is identified by its own <c>Id</c>,
    /// not by a <c>TeamId</c> pointing elsewhere, and because joining a squad has to find a
    /// team the caller is not yet scoped to. Access to teams is instead gated by the join
    /// code in the join handler and by the session token everywhere else.
    /// </para>
    /// <para>
    /// Legitimate cross-team reads, such as organisation-wide rollups, opt out explicitly
    /// with <c>IgnoreQueryFilters()</c> so that each one is visible in review.
    /// </para>
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
                //
                // Reading ActiveTeamId through a property access on the context instance keeps
                // the filter dynamic. EF parameterises it per query, so a scoped context picks
                // up the team of the request it was created for.
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
        // Seeded and imported rows carry their own historical timestamp, which the delivery
        // metrics depend on. Only rows that left it unset get stamped with the current time.
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

        // Team ownership is assigned from the session, never from the request body.
        // Overwriting rather than defaulting is what stops a caller from planting a row in
        // another squad by including a TeamId in the payload they post.
        //
        // Team rows are exempt: a team owns itself, and its TeamId stays null.
        if (activeTeamId.HasValue && entry.Entity is not Team)
        {
            entry.Entity.TeamId = activeTeamId;
        }
    }

    private static void StampUpdate(EntityEntry<BaseEntity> entry, DateTime now, string currentUser)
    {
        entry.Entity.UpdatedAtUtc = now;
        entry.Entity.UpdatedBy = currentUser;

        // Creation metadata and team ownership are immutable after insert. Marking them
        // unmodified means an update cannot rewrite history or move a row between squads,
        // even if something upstream mutated the tracked entity.
        // TeamMember is exempt because members can be assigned/reassigned to squads.
        entry.Property(nameof(BaseEntity.CreatedAtUtc)).IsModified = false;
        entry.Property(nameof(BaseEntity.CreatedBy)).IsModified = false;
        if (entry.Entity is not TeamMember)
        {
            entry.Property(nameof(BaseEntity.TeamId)).IsModified = false;
        }
    }
}
