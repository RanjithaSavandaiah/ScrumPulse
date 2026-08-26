namespace ScrumPulse.Infrastructure.Repositories;

using System.Collections.Concurrent;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Domain.Common;
using ScrumPulse.Infrastructure.Persistence;
using ScrumPulse.Infrastructure.Services;

public class EfUnitOfWork(AppDbContext db, DomainEventDispatcher eventDispatcher) : IUnitOfWork
{
    private readonly ConcurrentDictionary<Type, object> _repositories = new();

    public IAsyncRepository<T> Repository<T>() where T : BaseEntity
    {
        return (IAsyncRepository<T>)_repositories.GetOrAdd(typeof(T), _ => new EfRepository<T>(db));
    }

    public async Task<int> CommitAsync(CancellationToken ct = default)
    {
        // Extract all uncommitted domain events
        var entitiesWithEvents = db.ChangeTracker.Entries<BaseEntity>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Any())
            .ToList();

        var events = entitiesWithEvents.SelectMany(entity => entity.DomainEvents).ToList();

        var result = await db.SaveChangesAsync(ct);

        // Clear and dispatch domain events
        foreach (var entity in entitiesWithEvents)
        {
            entity.ClearDomainEvents();
        }

        foreach (var domainEvent in events)
        {
            await eventDispatcher.DispatchAsync(domainEvent, ct);
        }

        return result;
    }

    public Task RollbackAsync(CancellationToken ct = default)
    {
        foreach (var entry in db.ChangeTracker.Entries())
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

    public void Dispose()
    {
        db.Dispose();
    }
}
