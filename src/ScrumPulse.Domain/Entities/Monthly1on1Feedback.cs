namespace ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Common;

public class Monthly1on1Feedback : BaseEntity
{
    public Guid TeamMemberId { get; set; }
    public TeamMember? TeamMember { get; set; }

    public string MonthYear { get; set; } = string.Empty;
    public string ScrumMasterFeedback { get; set; } = string.Empty;
    public string CdlFeedback { get; set; } = string.Empty;
    public string ClientFeedback { get; set; } = string.Empty;
    public string SelfReflection { get; set; } = string.Empty;

    public int SmRating { get; set; } = 8;
    public int HappinessIndex { get; set; } = 8;

    public string ActionItems { get; set; } = string.Empty;
    public string NextMonthGoals { get; set; } = string.Empty;

    public string? AiSynthesizedStrengths { get; set; }
    public string? AiGrowthRecommendations { get; set; }
    public string? AiBurnoutRiskAssessment { get; set; }
}
