namespace ScrumPulse.Domain.Entities;

using ScrumPulse.Domain.Common;

public class TechDebtItem : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public int EstimatedHours { get; set; } = 8;
    public string Status { get; set; } = "Identified";
    public Guid? PayoffSprintId { get; set; }
    public Guid? AssigneeId { get; set; }
    public TeamMember? Assignee { get; set; }
}
