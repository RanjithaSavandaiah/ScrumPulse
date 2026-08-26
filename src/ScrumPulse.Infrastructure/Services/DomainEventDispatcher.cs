namespace ScrumPulse.Infrastructure.Services;

using Microsoft.Extensions.Logging;
using ScrumPulse.Domain.Events;

public class DomainEventDispatcher(ILogger<DomainEventDispatcher> logger)
{
    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        logger.LogInformation("Dispatched domain event {EventType} (ID: {EventId}) occurred at {Timestamp}",
            domainEvent.GetType().Name, domainEvent.EventId, domainEvent.OccurredAtUtc);
        return Task.CompletedTask;
    }
}
