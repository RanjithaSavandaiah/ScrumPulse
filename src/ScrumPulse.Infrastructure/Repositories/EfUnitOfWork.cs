namespace ScrumPulse.Infrastructure.Repositories;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Domain.Common;
using ScrumPulse.Infrastructure.Persistence;
using ScrumPulse.Infrastructure.Services;

/// <summary>
/// Unit of Work wrapping EF Core change tracking, repository caching,
/// and post commit domain event dispatch.
/// Does NOT dispose AppDbContext DI container owns that lifecycle.
/// </summary>
public class EfUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;
    private readonly DomainEventDispatcher _eventDispatcher;
    private readonly ILogger<EfUnitOfWork> _logger;
    private readonly ConcurrentDictionary<Type, object> _repositories = new();
    private bool _disposed;

    public EfUnitOfWork(AppDbContext db, DomainEventDispatcher eventDispatcher, ILogger<EfUnitOfWork> logger)
    {
        _db = db;
        _eventDispatcher = eventDispatcher;
        _logger = logger;
    }

    public IAsyncRepository<T> Repository<T>() where T : BaseEntity
    {
        return (IAsyncRepository<T>)_repositories.GetOrAdd(typeof(T), _ => new EfRepository<T>(_db));
    }

    public async Task<int> CommitAsync(CancellationToken ct = default)
    {
        // Extract all uncommitted domain events before save
        var entitiesWithEvents = _db.ChangeTracker.Entries<BaseEntity>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToList();

        var events = entitiesWithEvents.SelectMany(entity => entity.DomainEvents).ToList();

        var result = await _db.SaveChangesAsync(ct);

        _logger.LogDebug("UnitOfWork committed {ChangeCount} changes, dispatching {EventCount} domain events",
            result, events.Count);

        // Clear and dispatch domain events after successful commit
        foreach (var entity in entitiesWithEvents)
        {
            entity.ClearDomainEvents();
        }

        foreach (var domainEvent in events)
        {
            await _eventDispatcher.DispatchAsync(domainEvent, ct);
        }

        return result;
    }

    public Task RollbackAsync(CancellationToken ct = default)
    {
        foreach (var entry in _db.ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case Microsoft.EntityFrameworkCore.EntityState.Modified:
                case Microsoft.EntityFrameworkCore.EntityState.Deleted:
                    entry.Reload();
                    break;
                case Microsoft.EntityFrameworkCore.EntityState.Added:
                    entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                    break;
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears the repository cache. Does NOT dispose the DbContext
    /// the DI container manages that lifecycle to prevent double-dispose.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _repositories.Clear();
        // NOTE: Do NOT call _db.Dispose() here the DI container manages
        // the AppDbContext lifetime (Scoped). Disposing here causes double-dispose
        // when both UoW and DbContext are Scoped registrations.
    }
}
