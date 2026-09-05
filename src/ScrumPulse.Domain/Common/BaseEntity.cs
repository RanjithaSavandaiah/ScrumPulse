namespace ScrumPulse.Domain.Common;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ScrumPulse.Domain.Events;

/// <summary>
/// Base entity providing identity, audit trail, soft delete, optimistic concurrency,
/// and domain event support for all aggregate roots and entities.
/// </summary>
public abstract class BaseEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Multi tenant team identifier for squad isolation.</summary>
    public Guid? TeamId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Audit trail: identity or system that created this record.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Audit trail: identity or system that last modified this record.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>Soft delete flag. Entities with IsDeleted=true are filtered out by default query filters.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Optimistic concurrency token for conflict detection.</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    private readonly List<IDomainEvent> _domainEvents = [];

    [NotMapped]
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
