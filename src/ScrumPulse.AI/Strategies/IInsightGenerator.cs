namespace ScrumPulse.AI.Strategies;

using ScrumPulse.Application.DTOs;

/// <summary>
/// Strategy interface for AI insight generation.
/// Each level (Individual/Project/Company) implements its own
/// data aggregation and prompt construction strategy.
/// </summary>
public interface IInsightGenerator
{
    /// <summary>Level identifier (Individual, Project, Company).</summary>
    string Level { get; }

    /// <summary>Generates AI coaching insights for the specified context.</summary>
    Task<AiSuggestionResponse> GenerateAsync(InsightContext context, CancellationToken ct = default);
}

/// <summary>
/// Shared context object carrying all pre-fetched data needed for insight generation.
/// Implements the Agentic RAG pattern: data is retrieved first, then injected into prompts.
/// </summary>
public class InsightContext
{
    public Guid? MemberId { get; set; }
    public Guid? SprintId { get; set; }
    public string? MemberName { get; set; }
    public string? SprintName { get; set; }

    // ── Pre-aggregated Metrics (RAG Context) ─────────────────────────────
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
}
