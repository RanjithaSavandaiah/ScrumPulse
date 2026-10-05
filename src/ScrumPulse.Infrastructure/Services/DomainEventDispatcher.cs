namespace ScrumPulse.Infrastructure.Services;

using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScrumPulse.Domain.Events;

/// <summary>
/// Interface for domain event handlers. Register concrete implementations
/// in the DI container to handle specific domain events.
/// </summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct = default);
}

/// <summary>
/// Dispatches domain events to all registered handlers via the DI container.
/// Uses structured logging for observability.
/// Caches reflected MethodInfo per handler type to avoid per-dispatch reflection overhead.
/// </summary>
public class DomainEventDispatcher(IServiceProvider serviceProvider, ILogger<DomainEventDispatcher> logger)
{
    /// <summary>Cached HandleAsync MethodInfo per handler interface type to avoid repeated reflection.</summary>
    private static readonly ConcurrentDictionary<Type, MethodInfo?> _methodCache = new();

    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        var eventType = domainEvent.GetType();
        var handlerInterfaceType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);

        var handlers = serviceProvider.GetServices(handlerInterfaceType);
        var handlerList = handlers.ToList();

        logger.LogInformation(
            "Dispatching domain event {EventType} (ID: {EventId}, At: {Timestamp}) to {HandlerCount} handler(s)",
            eventType.Name, domainEvent.EventId, domainEvent.OccurredAtUtc, handlerList.Count);

        // Resolve and cache the HandleAsync method once per handler interface type
        var method = _methodCache.GetOrAdd(handlerInterfaceType, type => type.GetMethod("HandleAsync"));

        foreach (var handler in handlerList)
        {
            try
            {
                if (method != null)
                {
                    var task = (Task)method.Invoke(handler, [domainEvent, ct])!;
                    await task;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Domain event handler failed for {EventType} (ID: {EventId})",
                    eventType.Name, domainEvent.EventId);
                // Don't rethrow domain event handler failures should not break the primary flow
            }
        }
    }
}

