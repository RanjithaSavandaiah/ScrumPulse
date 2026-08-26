namespace ScrumPulse.Domain.Entities;

using ScrumPulse.Domain.Common;

public class TechTalkLog : BaseEntity
{
    public string Topic { get; set; } = string.Empty;
    public Guid PresenterId { get; set; }
    public TeamMember? Presenter { get; set; }
    public DateTime TalkDate { get; set; } = DateTime.UtcNow;
    public int DurationMinutes { get; set; } = 30;
    public string? KeyTakeaways { get; set; }
    public string? SlidesUrl { get; set; }
}
