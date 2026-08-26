namespace ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Common;

public class DailyStandup : BaseEntity
{
    public Guid TeamMemberId { get; set; }
    public TeamMember? TeamMember { get; set; }

    public Guid? SprintId { get; set; }
    public Sprint? Sprint { get; set; }

    public DateTime StandupDate { get; set; } = DateTime.UtcNow.Date;
    public string YesterdaySummary { get; set; } = string.Empty;
    public string TodayPlan { get; set; } = string.Empty;
    public string? BlockersText { get; set; }
    public int MoodScore { get; set; } = 4;
}
