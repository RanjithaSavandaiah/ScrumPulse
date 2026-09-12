namespace ScrumPulse.AI.Strategies;

using ScrumPulse.Application.DTOs;

/// <summary>
/// AI insight generator strategy for sprint predictability, risk radar, and blockers.
/// </summary>
public class SprintInsightGenerator : IInsightGenerator
{
    public string Level => "Project";

    public Task<AiSuggestionResponse> GenerateAsync(InsightContext ctx, CancellationToken ct = default)
    {
        if (ctx.CommittedPoints == 0 && ctx.DeliveredPoints == 0 && ctx.ActiveBlockers == 0)
        {
            var noDataFindings = new List<string>
            {
                $"[NO DATA TO ANALYZE - Sprint Backlog]: {ctx.SprintName} has 0 committed story points recorded.",
                "[NO DATA TO ANALYZE - Delivery]: No completed work items or velocity registered for this sprint yet.",
                "[NO DATA TO ANALYZE - Impediments]: No blocker tracking or standup health data recorded for this sprint."
            };
            var noDataRecs = new List<string>
            {
                "Estimate user stories and commit story points to this sprint backlog.",
                "Update work item statuses as development proceeds to generate Say-Do predictability.",
                "Track blockers and daily standups to evaluate sprint delivery risks."
            };
            return Task.FromResult(new AiSuggestionResponse(
                "Project",
                $"Microsoft AI Sprint Risk Radar & Executive Insights: {ctx.SprintName}",
                $"Awaiting sprint backlog telemetry for {ctx.SprintName}.",
                noDataFindings, noDataRecs,
                "No Data (Telemetry Pending)",
                DateTime.UtcNow
            ));
        }

        int sayDoPercent = ctx.CommittedPoints > 0 ? (int)((ctx.DeliveredPoints / (double)ctx.CommittedPoints) * 100) : 0;

        var findings = new List<string>
        {
            $"[STRENGTH - Velocity & Say-Do]: Sprint Say-Do delivery tracking at {sayDoPercent}% ({ctx.DeliveredPoints}/{ctx.CommittedPoints} Story Points completed).",
            $"[STRENGTH - Continuous Learning]: {ctx.TotalTechTalks} Weekly Tech Talks conducted across the team.",
            $"[TEAM - SQUAD CAPACITY]: {ctx.TotalLeaveDays:0.#} team leave days recorded during this sprint window.",
            $"[COMMS - TEAM HEALTH & 1:1 PULSE]: Average Squad Happiness is {Math.Round(ctx.AvgTeamHappiness, 1)}/10; Average SM Performance Rating is {Math.Round(ctx.AvgSmRating, 1)}/10.",
            ctx.ActiveBlockers > 0
                ? $"[WARNING - Active Blockers]: {ctx.ActiveBlockers} active blocker(s) awaiting resolution (SLA monitoring active)."
                : "[STRENGTH - UNBLOCKED SQUAD]: Zero active blockers detected. Clear runway for sprint goal execution.",
            "[QUALITY - GATES]: Completed user stories should adhere to Definition of Ready (DoR) and Definition of Done (DoD) verification."
        };

        var recs = new List<string>
        {
            ctx.ActiveBlockers > 0
                ? "Escalate pending client blocker dependencies during morning golden overlap sync to protect sprint target date."
                : "Maintain current daily standup cadence to identify potential blockers before they breach SLA.",
            "Leverage auto-calculated net capacity in sprint planning to prevent over-commitment when team leaves are clustered.",
            "Continue institutionalizing weekly 30-min Tech Talks to cross-train squad members on critical checkout modules.",
            "Celebrate sprint delivery milestones on the Kudos Wall to reinforce team morale and recognition."
        };

        return Task.FromResult(new AiSuggestionResponse(
            "Project",
            $"Microsoft AI Sprint Risk Radar & Executive Insights: {ctx.SprintName}",
            $"Autonomous executive synthesis covering Velocity, Auto-Calculated Capacity, Tech Talks, Daily Standups, and 1:1 Feedback.",
            findings, recs,
            ctx.ActiveBlockers > 2 ? "High (Sprint Scope at Risk)" : (ctx.ConfidenceScore < 7 ? "Medium" : "Low (Optimal Flow)"),
            DateTime.UtcNow
        ));
    }
}
