namespace ScrumPulse.AI.Strategies;

using ScrumPulse.Application.DTOs;

/// <summary>
/// AI insight generator strategy for developer 1:1 coaching and performance telemetry.
/// </summary>
public class IndividualInsightGenerator : IInsightGenerator
{
    public string Level => "Individual";

    public Task<AiSuggestionResponse> GenerateAsync(InsightContext ctx, CancellationToken ct = default)
    {
        if (ctx.TotalAssigned == 0 && ctx.StandupCount == 0 && ctx.HappinessIndex == 0)
        {
            var noDataFindings = new List<string>
            {
                $"[NO DATA TO ANALYZE - Delivery]: No work items currently assigned to {ctx.MemberName}.",
                "[NO DATA TO ANALYZE - Standups]: Zero daily standup check-ins logged for this developer.",
                "[NO DATA TO ANALYZE - 1:1 Reviews]: 1:1 feedback and happiness score pending initial session."
            };
            var noDataRecs = new List<string>
            {
                $"Assign active user stories or tasks to {ctx.MemberName} in sprint planning.",
                "Log daily standups to capture execution progress and blocker impediments.",
                "Schedule the initial monthly 1:1 check-in to record baseline morale and growth goals."
            };
            return Task.FromResult(new AiSuggestionResponse(
                "Individual",
                $"Microsoft AI Coaching Plan & 360° Intelligence: {ctx.MemberName}",
                $"Awaiting telemetry for {ctx.MemberName} — metrics will populate as sprint activities and standups are logged.",
                noDataFindings, noDataRecs,
                "No Data (Telemetry Pending)",
                DateTime.UtcNow
            ));
        }

        var findings = new List<string>
        {
            $"[STRENGTH - Velocity & Delivery]: Delivered {ctx.CompletedItems}/{ctx.TotalAssigned} work items ({ctx.TotalStoryPoints} Story Points) with avg dev execution time of {Math.Round(ctx.AvgDevCycleHours, 1)}h.",
            $"[STRENGTH - Knowledge Sharing & Culture]: Delivered {ctx.TechTalksGiven} Weekly Tech Talk(s) and received {ctx.KudosReceived} team Kudos recognition(s).",
            $"[METRICS - CAPACITY & LEAVES]: {ctx.TotalLeaveDays:0.#} approved leave days recorded.",
            $"[COMMS - DAILY STANDUP & 1:1 ALIGNMENT]: {ctx.StandupCount} recent standup updates logged; SM Performance Rating is {ctx.SmRating}/10 and Happiness Index is {ctx.HappinessIndex}/10.",
            ctx.AvgReviewLatencyHours > 6.0
                ? $"[WARNING - PR Latency]: PR Code Review turnaround latency averages {Math.Round(ctx.AvgReviewLatencyHours, 1)}h (Exceeds SLA target of < 6h)."
                : $"[STRENGTH - Code Review SLA]: PR Review turnaround latency is optimal at {Math.Round(ctx.AvgReviewLatencyHours, 1)}h.",
            ctx.HappinessIndex < 7
                ? $"[WARNING - WELLBEING RADAR]: Happiness Index is {ctx.HappinessIndex}/10. Needs 1:1 check-in to mitigate burnout."
                : "[STRENGTH - MORALE & ENGAGEMENT]: High engagement score with proactive standup communication."
        };

        var recs = new List<string>
        {
            ctx.AvgReviewLatencyHours > 6.0
                ? "Prioritize daily 30-min golden review window during morning overlap to bring PR review turnaround below 4h."
                : "Continue mentoring peers in architectural code reviews during golden overlap hours.",
            ctx.TechTalksGiven == 0
                ? "Encourage scheduling a 30-min Offshore Tech Talk session on recent feature implementation or design patterns."
                : "Nominate for leading the upcoming sprint architecture spike session based on proven tech sharing.",
            ctx.TotalLeaveDays > 3.0
                ? "Coordinate with Scrum Master to calibrate sprint capacity and WIP limits to prevent post-leave overload."
                : "Maintain current focused WIP limit (< 3 active PBIs) to ensure zero context-switching overhead.",
            ctx.LastActionItems != null
                ? $"Follow up on agreed 1:1 action item: \"{ctx.LastActionItems}\"."
                : "Schedule monthly 1:1 touchpoint with CDL and Scrum Master to track personal career milestones."
        };

        return Task.FromResult(new AiSuggestionResponse(
            "Individual",
            $"Microsoft AI Coaching Plan & 360° Intelligence: {ctx.MemberName}",
            $"Holistic evaluation synthesizing Velocity ({ctx.TotalStoryPoints} pts), Net Capacity ({ctx.TotalLeaveDays}d leave), {ctx.TechTalksGiven} Tech Talks, Standups & 1:1 Feedback.",
            findings, recs,
            ctx.HappinessIndex < 6 ? "Medium (Burnout Sentinel Triggered)" : "Low (Healthy Morale & High Flow)",
            DateTime.UtcNow
        ));
    }
}
