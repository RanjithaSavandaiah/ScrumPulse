namespace ScrumPulse.AI.Prompt;

using ScrumPulse.AI.Configuration;
using ScrumPulse.AI.Strategies;

/// <summary>
/// Builds the instructions and prompts sent to the insight agents.
/// </summary>
/// <remarks>
/// Retrieved metrics are rendered as one labelled figure per line. That layout is what makes
/// <see cref="TokenBudget.TrimToBudget"/> safe to apply, and it gives the model an unambiguous
/// set of numbers to quote rather than a paragraph it has to parse.
/// </remarks>
public sealed class InsightPromptBuilder(AgentConfiguration configuration)
{
    /// <summary>
    /// System instructions shared by every insight agent.
    /// </summary>
    /// <remarks>
    /// The grounding rule is the important part. These answers are shown to a delivery lead
    /// as analysis of their squad, so a model that fills a gap with a plausible invented
    /// figure does real damage. It is told to name the gap instead.
    /// </remarks>
    public string BuildSystemInstructions(string analysisScope) => $$"""
        You are {{configuration.Persona}}.

        You are analysing {{analysisScope}} for a software delivery squad.

        Rules:
        - Use only the metrics given in the prompt. Never invent a number, a name, or an event.
        - If the metrics do not support a conclusion, say what is missing instead of guessing.
        - Quote the specific figure behind every finding.
        - Recommendations must be actions a Scrum Master can take within one sprint.
        - Be direct. No preamble, no restating the question, no motivational filler.

        Respond with JSON only, matching exactly this shape:
        {
          "title": "short headline",
          "summary": "two or three sentences on the overall position",
          "keyFindings": ["observation citing a figure", "..."],
          "recommendations": ["specific action", "..."],
          "riskLevel": "Low | Medium | High | Critical"
        }
        """;

    /// <summary>Builds the prompt for coaching analysis of one squad member.</summary>
    public string BuildIndividualPrompt(InsightContext context)
    {
        var metrics = $"""
            Member: {context.MemberName}
            Work items assigned: {context.TotalAssigned}
            Work items completed: {context.CompletedItems}
            Story points delivered: {context.TotalStoryPoints}
            Average development cycle time: {context.AvgDevCycleHours:F1} hours
            Average PR review turnaround: {context.AvgReviewLatencyHours:F1} hours (squad target is under 6)
            Approved leave: {context.TotalLeaveDays:F1} days
            Net capacity this sprint: {context.NetCapacityHours:F1} hours
            Standups logged recently: {context.StandupCount}
            Tech talks delivered: {context.TechTalksGiven}
            Kudos received: {context.KudosReceived}
            Happiness index: {FormatRating(context.HappinessIndex)}
            Scrum Master rating: {FormatRating(context.SmRating)}
            Actions from last one-to-one: {context.LastActionItems ?? "none recorded"}
            """;

        return ComposePrompt("Produce a coaching analysis for this squad member.", metrics);
    }

    /// <summary>Builds the prompt for sprint risk and delivery analysis.</summary>
    public string BuildSprintPrompt(InsightContext context)
    {
        var sayDoRatio = context.CommittedPoints > 0
            ? $"{context.DeliveredPoints / (double)context.CommittedPoints * 100:F0}%"
            : "not yet measurable, nothing committed";

        var metrics = $"""
            Sprint: {context.SprintName}
            Story points committed: {context.CommittedPoints}
            Story points delivered: {context.DeliveredPoints}
            Say-do ratio: {sayDoRatio}
            Unresolved blockers: {context.ActiveBlockers}
            Squad confidence: {FormatRating(context.ConfidenceScore)}
            Leave days across the squad: {context.TotalLeaveDays:F1}
            Average squad happiness: {FormatRating(context.AvgTeamHappiness)}
            Average Scrum Master rating: {FormatRating(context.AvgSmRating)}
            Tech talks delivered: {context.TotalTechTalks}
            """;

        return ComposePrompt(
            "Assess delivery risk for this sprint and recommend interventions.",
            metrics);
    }

    /// <summary>Builds the prompt for the squad-wide delivery trend analysis.</summary>
    public string BuildPortfolioPrompt(InsightContext context)
    {
        var completionRate = context.TotalWorkItems > 0
            ? $"{context.CompletedItems / (double)context.TotalWorkItems * 100:F0}%"
            : "not yet measurable, no work items recorded";

        var blockerResolutionRate = context.TotalBlockers > 0
            ? $"{context.ResolvedBlockers / (double)context.TotalBlockers * 100:F0}%"
            : "not yet measurable, no blockers recorded";

        var metrics = $"""
            Sprints recorded: {context.SprintsCount}
            Work items total: {context.TotalWorkItems}
            Work items completed: {context.CompletedItems} ({completionRate})
            Story points delivered: {context.TotalStoryPoints}
            Defects that escaped to production: {context.EscapedDefects}
            Blockers raised: {context.TotalBlockers}
            Blockers resolved: {context.ResolvedBlockers} ({blockerResolutionRate})
            Blockers still open: {context.ActiveBlockers}
            Kudos exchanged: {context.TotalKudos}
            Tech talks delivered: {context.TotalTechTalks}
            """;

        return ComposePrompt(
            "Identify delivery trends across these sprints and what leadership should act on.",
            metrics);
    }

    /// <summary>
    /// Joins the task and its metrics, trimming the metrics to the context budget.
    /// </summary>
    private string ComposePrompt(string task, string metrics)
    {
        var budgetForMetrics = configuration.EffectiveContextTokens - TokenBudget.Estimate(task);
        var trimmedMetrics = TokenBudget.TrimToBudget(metrics, budgetForMetrics);

        return $"""
            {task}

            Metrics:
            {trimmedMetrics}
            """;
    }

    /// <summary>
    /// Renders a nought-to-ten rating, distinguishing "not yet rated" from a genuine zero.
    /// </summary>
    /// <remarks>
    /// Both arrive as 0 from the database. Printing an unrated member as "0/10" invites the
    /// model to report a wellbeing crisis that the data never claimed.
    /// </remarks>
    private static string FormatRating(double rating) =>
        rating <= 0 ? "not yet rated" : $"{rating:F1} out of 10";
}
