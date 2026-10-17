namespace ScrumPulse.AI.Strategies;

using ScrumPulse.Application.DTOs;

/// <summary>
/// Produces a rule-based delivery analysis for one scope, with no model involved.
/// </summary>
/// <remarks>
/// <para>
/// These implementations are the fallback the coach serves whenever live inference is not
/// available: no API key configured, the daily free-tier allowance spent, the provider down,
/// or a generated answer that failed its quality gate. They restate the retrieved metrics in
/// plain language. Less insightful than a model, but immediate, free, and incapable of
/// inventing a figure.
/// </para>
/// <para>
/// Output from these generators is never described to the user as model-generated, because
/// it is not. The service logs which path produced each response.
/// </para>
/// </remarks>
public interface IInsightGenerator
{
    /// <summary>Scope this generator covers: Individual, Project, or Company.</summary>
    string Level { get; }

    /// <summary>Builds the analysis from already-retrieved metrics.</summary>
    /// <remarks>
    /// Synchronous by design. There is no I/O here: retrieval has already happened and this
    /// is pure computation over the supplied context. An async signature would have implied
    /// otherwise and forced every caller into a state machine for nothing.
    /// </remarks>
    AiSuggestionResponse Generate(InsightContext context);
}

/// <summary>
/// Metrics retrieved for one analysis scope, shared by the prompt builder, the rule-based
/// generators, and the copilot's tools.
/// </summary>
/// <remarks>
/// Populated by <c>IDeliveryMetricsRetriever</c>. Which fields carry meaning depends on the
/// scope: member analysis fills the delivery and wellbeing fields, sprint analysis the
/// commitment fields, portfolio analysis the cumulative ones. Unset fields stay at zero,
/// which the prompt builder renders as "not yet measurable" rather than as a real zero.
/// </remarks>
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
    public int TotalWorkItems { get; set; }
    public int EscapedDefects { get; set; }
    public int TotalBlockers { get; set; }
    public int ResolvedBlockers { get; set; }
    public int TotalKudos { get; set; }
    public int SprintsCount { get; set; }
}
