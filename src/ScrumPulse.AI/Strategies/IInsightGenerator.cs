namespace ScrumPulse.AI.Strategies;

using ScrumPulse.Application.DTOs;

/// <summary>
/// Rule-based delivery analysis, the fallback whenever inference is unavailable. Its output is
/// never presented to the user as model-generated.
/// </summary>
public interface IInsightGenerator
{
    // Individual, Project, or Company.
    string Level { get; }

    // Synchronous: retrieval already happened, so there is no I/O to await here.
    AiSuggestionResponse Generate(InsightContext context);
}

/// <summary>
/// Metrics for one analysis scope. Which fields carry meaning depends on the scope; unset ones
/// stay at zero, which the prompt builder renders as "not yet measurable".
/// </summary>
public class InsightContext
{
    public Guid? MemberId { get; set; }
    public Guid? SprintId { get; set; }
    public string? MemberName { get; set; }
    public string? SprintName { get; set; }

    public int TotalAssigned { get; set; }
    public int CompletedItems { get; set; }
    public int TotalStoryPoints { get; set; }
    public double TotalLeaveDays { get; set; }
    public double NetCapacityHours { get; set; }
    public int StandupCount { get; set; }
    public int TechTalksGiven { get; set; }
    public int KudosReceived { get; set; }
    public double AvgDevCycleHours { get; set; }
    public double AvgReviewLatencyHours { get; set; }
    public int HappinessIndex { get; set; }
    public int SmRating { get; set; }
    public int ActiveBlockers { get; set; }
    public int CommittedPoints { get; set; }
    public int DeliveredPoints { get; set; }
    public int ConfidenceScore { get; set; }
    public double AvgTeamHappiness { get; set; }
    public double AvgSmRating { get; set; }
    public string? LastActionItems { get; set; }
    public int TotalTechTalks { get; set; }
    public int TotalWorkItems { get; set; }
    public int EscapedDefects { get; set; }
    public int TotalBlockers { get; set; }
    public int ResolvedBlockers { get; set; }
    public int TotalKudos { get; set; }
    public int SprintsCount { get; set; }
}
