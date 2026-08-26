namespace ScrumPulse.Domain.Entities;

using ScrumPulse.Domain.Common;

public class RetroActionItem : BaseEntity
{
    public Guid? SprintId { get; set; }
    public Sprint? Sprint { get; set; }

    public string Title { get; set; } = string.Empty;
    public Guid? AssigneeId { get; set; }
    public TeamMember? Assignee { get; set; }

    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; } = false;
}
