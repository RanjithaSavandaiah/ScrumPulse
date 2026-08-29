namespace ScrumPulse.Domain.Entities;

using ScrumPulse.Domain.Common;
using ScrumPulse.Domain.Enums;

/// <summary>
/// Tracks technical debt items with severity classification and resolution lifecycle.
/// </summary>
public class TechDebtItem : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TechDebtSeverity Severity { get; set; } = TechDebtSeverity.Medium;
    public int EstimatedHours { get; set; } = 8;
    public TechDebtStatus Status { get; set; } = TechDebtStatus.Identified;
    public Guid? PayoffSprintId { get; set; }
    public Guid? AssigneeId { get; set; }
    public TeamMember? Assignee { get; set; }
}
