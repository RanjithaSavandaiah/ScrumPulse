namespace ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Common;

public class Sprint : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public int CommittedStoryPoints { get; set; }
    public int DeliveredStoryPoints { get; set; }
    public int ConfidenceScore { get; set; } = 8;
    public string? ConfidenceNotes { get; set; }
    public double DailyWorkingHours { get; set; } = 8.5;

    public ICollection<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
    public ICollection<Blocker> Blockers { get; set; } = new List<Blocker>();
}
