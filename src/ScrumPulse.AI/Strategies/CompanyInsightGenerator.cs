namespace ScrumPulse.AI.Strategies;

using ScrumPulse.Application.DTOs;

/// <summary>
/// AI insight generator strategy for enterprise-wide strategic intelligence across squads.
/// </summary>
public class CompanyInsightGenerator : IInsightGenerator
{
    public string Level => "Company";

    public Task<AiSuggestionResponse> GenerateAsync(InsightContext ctx, CancellationToken ct = default)
    {
        if (ctx.SprintsCount == 0 && ctx.TotalWorkItems == 0)
        {
            var noDataFindings = new List<string>
            {
                "[NO DATA TO ANALYZE - Sprints]: 0 completed sprints recorded in the database. Delivery velocity trends and cross-sprint predictability cannot be evaluated yet.",
                "[NO DATA TO ANALYZE - Work Items]: 0 user stories or tasks tracked. Production defect escape ratios and cycle times are unavailable.",
                "[NO DATA TO ANALYZE - Collaboration]: Daily standups, blocker resolution SLAs, and retrospective items have not been initialized."
            };

            var noDataRecs = new List<string>
            {
                "Create and activate a sprint in Work Items & Lifecycle to begin capturing agile velocity and commitment data.",
                "Add user stories and development tasks with story points to establish Say-Do predictability telemetry.",
                "Record daily standup check-ins and log blockers to generate distributed collaboration intelligence."
            };

            return Task.FromResult(new AiSuggestionResponse(
                "Company",
                "Microsoft Agent Strategic Distributed Collaboration Intelligence",
                "Enterprise-level analysis for CDL, Scrum Master, and Client leadership (Awaiting telemetry).",
                noDataFindings,
                noDataRecs,
                "No Data (Telemetry Pending)",
                DateTime.UtcNow
            ));
        }

        int totalItems = ctx.TotalWorkItems;
        int doneItems = ctx.CompletedItems;
        int totalPoints = ctx.TotalStoryPoints;
        int escapedBugs = ctx.EscapedDefects;
        double defectRate = totalItems > 0 ? Math.Round((double)escapedBugs / totalItems * 100, 1) : 0;

        int totalBlockers = ctx.TotalBlockers;
        int activeBlockers = ctx.ActiveBlockers;
        int resolvedBlockers = ctx.ResolvedBlockers;
        double blockerSla = totalBlockers > 0 ? Math.Round((double)resolvedBlockers / totalBlockers * 100, 1) : 100;

        var findings = new List<string>();

        if (totalPoints > 0)
        {
            findings.Add($"[STRENGTH - Velocity & Delivery]: Team has delivered {totalPoints} story points across {totalItems} work items ({doneItems} completed).");
        }
        else
        {
            findings.Add($"[METRICS - Delivery Flow]: {totalItems} work items tracked in backlog with {doneItems} completed.");
        }

        if (escapedBugs == 0)
        {
            findings.Add("[QUALITY - Defect Escape]: Zero escaped defects in production across current delivery telemetry.");
        }
        else
        {
            findings.Add($"[WARNING - Quality]: Escaped defect rate is {defectRate}% ({escapedBugs} escaped defects across {totalItems} items).");
        }

        if (totalBlockers > 0)
        {
            findings.Add($"[RISK - Blocker SLA]: Blocker resolution compliance is at {blockerSla}% ({resolvedBlockers}/{totalBlockers} resolved). Active blockers: {activeBlockers}.");
        }
        else
        {
            findings.Add("[STRENGTH - Impediment Pipeline]: Zero active blockers detected. Delivery runway is currently unblocked.");
        }

        if (ctx.TotalKudos > 0 || ctx.TotalTechTalks > 0)
        {
            findings.Add($"[CULTURE - Engineering]: {ctx.TotalTechTalks} tech sharing sessions delivered and {ctx.TotalKudos} peer kudos recognitions awarded.");
        }
        else
        {
            findings.Add("[CULTURE - Baseline]: Culture and knowledge sharing telemetry initializing — log Tech Talks and Kudos to track collaboration index.");
        }

        var recs = new List<string>();
        if (activeBlockers > 0)
        {
            recs.Add($"Prioritize resolving the {activeBlockers} active blocker(s) during morning overlap hours to protect sprint commitments.");
        }
        else
        {
            recs.Add("Maintain proactive daily standup identification of dependencies to keep the blocker runway clear.");
        }

        if (escapedBugs > 0)
        {
            recs.Add("Implement Definition of Ready (DoR) and Definition of Done (DoD) verification gates to eliminate escaped defects.");
        }
        else
        {
            recs.Add("Continue rigorous code review practices and automated test coverage to preserve zero-defect production delivery.");
        }

        if (ctx.TotalTechTalks == 0)
        {
            recs.Add("Institutionalize 30-min bi-weekly tech sharing sessions to foster knowledge sharing across squads.");
        }
        else
        {
            recs.Add("Showcase automated Say-Do predictability and quality metrics in upcoming stakeholder reviews.");
        }

        if (ctx.TotalKudos == 0)
        {
            recs.Add("Encourage squad recognition on the Appreciation Wall to build team engagement and retention.");
        }
        else
        {
            recs.Add("Leverage peer kudos recognitions in quarterly talent appraisals to reinforce high collaboration.");
        }

        string riskLevel = activeBlockers >= 3 || defectRate > 5.0
            ? "High (Impediments / Quality Attention Required)"
            : (activeBlockers > 0 || defectRate > 2.0 ? "Medium (Moderate Blocker Activity)" : "Low (Optimal Delivery Flow)");

        var response = new AiSuggestionResponse(
            "Company",
            "Microsoft Agent Strategic Distributed Collaboration Intelligence",
            "Enterprise-level analysis for CDL, Scrum Master, and Client leadership.",
            findings,
            recs,
            riskLevel,
            DateTime.UtcNow
        );

        return Task.FromResult(response);
    }
}
