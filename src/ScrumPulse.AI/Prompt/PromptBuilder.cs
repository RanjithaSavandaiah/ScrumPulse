namespace ScrumPulse.AI.Prompt;

using ScrumPulse.AI.Configuration;
using ScrumPulse.AI.Strategies;

/// <summary>
/// Token-aware prompt builder implementing the Agentic RAG pattern.
/// Constructs structured prompts by injecting pre-aggregated context data
/// while respecting token budget constraints and headroom.
/// </summary>
public class PromptBuilder(AgentConfiguration config)
{
    /// <summary>
    /// Builds a structured prompt for individual coaching analysis.
    /// Optimizes token usage by selecting the most relevant data points.
    /// </summary>
    public string BuildIndividualPrompt(InsightContext ctx)
    {
        return $"""
        [SYSTEM] You are a {config.Persona}.
        [TOKEN_BUDGET] Max {config.EffectiveContextTokens} context tokens, {config.MaxResponseTokens} response tokens.

        [RAG_CONTEXT - Individual Analysis: {ctx.MemberName}]
        Velocity: {ctx.CompletedItems}/{ctx.TotalAssigned} items completed ({ctx.TotalStoryPoints} story points)
        Dev Cycle Time: {ctx.AvgDevCycleHours:F1}h avg | PR Review Latency: {ctx.AvgReviewLatencyHours:F1}h avg
        Capacity: {ctx.TotalLeaveDays:F1} leave days recorded | Net hours: {Math.Max(0, 85.0 - (ctx.TotalLeaveDays * 8.5)):F1}h
        Engagement: {ctx.StandupCount} standups | {ctx.TechTalksGiven} tech talks | {ctx.KudosReceived} kudos received
        Wellbeing: Happiness {ctx.HappinessIndex}/10 | SM Rating {ctx.SmRating}/10
        Last 1:1 Actions: {ctx.LastActionItems ?? "No prior action items recorded"}

        [INSTRUCTION] Generate coaching findings (strengths & growth areas) and actionable recommendations.
        Focus on data-driven insights. Flag burnout risk if happiness < 7.
        """;
    }

    /// <summary>
    /// Builds a structured prompt for sprint/project analysis.
    /// </summary>
    public string BuildSprintPrompt(InsightContext ctx)
    {
        int sayDoPercent = ctx.CommittedPoints > 0
            ? (int)((ctx.DeliveredPoints / (double)ctx.CommittedPoints) * 100) : 0;

        return $"""
        [SYSTEM] You are a {config.Persona}.
        [TOKEN_BUDGET] Max {config.EffectiveContextTokens} context tokens, {config.MaxResponseTokens} response tokens.

        [RAG_CONTEXT - Sprint Analysis: {ctx.SprintName}]
        Say-Do Ratio: {sayDoPercent}% ({ctx.DeliveredPoints}/{ctx.CommittedPoints} story points)
        Active Blockers: {ctx.ActiveBlockers} | Confidence: {ctx.ConfidenceScore}/10
        Team Capacity: {ctx.TotalLeaveDays:F1} total leave days deducted
        Health Pulse: Avg Happiness {ctx.AvgTeamHappiness:F1}/10 | Avg SM Rating {ctx.AvgSmRating:F1}/10
        Knowledge Sharing: {ctx.TotalTechTalks} tech talks conducted

        [INSTRUCTION] Generate sprint risk assessment, velocity analysis, and actionable recommendations.
        Highlight blockers and capacity risks. Suggest interventions.
        """;
    }

    /// <summary>Estimates approximate token count for a string (4 chars ≈ 1 token).</summary>
    public static int EstimateTokens(string text) => text.Length / 4;
}
