namespace ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Common;
using ScrumPulse.Domain.Enums;

public class Blocker : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BlockerCategory Category { get; set; } = BlockerCategory.ClientClarification;
    public int SlaHoursLimit { get; set; } = 8;

    public Guid? WorkItemId { get; set; }
    public WorkItem? WorkItem { get; set; }

    public Guid? RaisedById { get; set; }
    public TeamMember? RaisedBy { get; set; }

    public Guid? SprintId { get; set; }
    public Sprint? Sprint { get; set; }

    public DateTime RaisedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolutionNotes { get; set; }

    public bool IsResolved => ResolvedAtUtc.HasValue;
    public double HoursWaiting => Math.Round(((ResolvedAtUtc ?? DateTime.UtcNow) - RaisedAtUtc).TotalHours, 1);
    public bool IsSlaBreached => HoursWaiting > SlaHoursLimit && !IsResolved;
}
