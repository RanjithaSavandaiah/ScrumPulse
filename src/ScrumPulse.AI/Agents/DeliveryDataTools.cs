namespace ScrumPulse.AI.Agents;

using Microsoft.Extensions.AI;
using ScrumPulse.AI.Retrieval;

/// <summary>
/// Exposes the squad's delivery data to the copilot as callable tools.
/// </summary>
/// <remarks>
/// <para>
/// This is the retrieval half of the agentic RAG loop. Rather than deciding up front which
/// metrics a question needs and pasting them all into the prompt, the assistant is given
/// these functions and fetches what it actually needs. A question about one blocker costs
/// one small tool call instead of the squad's entire metric set, which is what makes the
/// pattern affordable on a per-day request allowance.
/// </para>
/// <para>
/// Every tool reads through <see cref="IDeliveryMetricsRetriever"/>, whose queries run under
/// the tenant filter. The assistant therefore cannot reach another squad's data even if a
/// question asks it to, because there is no parameter through which a team could be named.
/// </para>
/// <para>
/// The tools are read-only by design. Nothing here writes, so a prompt-injected instruction
/// in a blocker title cannot make the assistant change or delete anything.
/// </para>
/// </remarks>
public static class DeliveryDataTools
{
    /// <summary>Most blockers returned in one call, to keep a tool result inside the budget.</summary>
    private const int MaximumBlockersReturned = 10;

    /// <summary>
    /// Builds the tool set bound to a retriever for the current request's squad.
    /// </summary>
    public static IReadOnlyList<AITool> CreateFor(IDeliveryMetricsRetriever retriever) =>
    [
        AIFunctionFactory.Create(
            async (CancellationToken ct) =>
            {
                var sprint = await retriever.BuildActiveSprintContextAsync(ct);

                if (sprint is null)
                {
                    return "No sprint is currently active for this squad.";
                }

                var sayDoRatio = sprint.CommittedPoints > 0
                    ? $"{sprint.DeliveredPoints / (double)sprint.CommittedPoints * 100:F0}%"
                    : "not measurable, nothing committed";

                return $"""
                    Sprint: {sprint.SprintName}
                    Committed story points: {sprint.CommittedPoints}
                    Delivered story points: {sprint.DeliveredPoints}
                    Say-do ratio: {sayDoRatio}
                    Unresolved blockers: {sprint.ActiveBlockers}
                    Squad confidence: {sprint.ConfidenceScore} out of 10
                    Leave days booked across the squad: {sprint.TotalLeaveDays:F1}
                    """;
            },
            name: "get_active_sprint_status",
            description: "Returns commitment, delivery, blocker count and confidence for the sprint currently in flight."),

        AIFunctionFactory.Create(
            async (CancellationToken ct) =>
            {
                var blockers = await retriever.GetOpenBlockersAsync(MaximumBlockersReturned, ct);

                if (blockers.Count == 0)
                {
                    return "No blockers are currently open.";
                }

                var lines = blockers.Select(blocker =>
                    $"- {blocker.Title} | cause: {blocker.Category} | waiting {blocker.HoursWaiting:F1}h" +
                    (blocker.BreachesSla ? " | SLA BREACHED" : string.Empty));

                return string.Join('\n', lines);
            },
            name: "get_open_blockers",
            description: "Lists blockers that are still unresolved, longest waiting first, with their root cause and whether they have breached SLA."),

        AIFunctionFactory.Create(
            async (string memberName, CancellationToken ct) =>
            {
                var member = await retriever.FindMemberByNameAsync(memberName, ct);

                if (member is null)
                {
                    return $"No squad member matching '{memberName}' was found.";
                }

                var metrics = await retriever.BuildMemberContextAsync(member.Value.Id, ct);

                return $"""
                    Member: {metrics.MemberName}
                    Work items assigned: {metrics.TotalAssigned}
                    Work items completed: {metrics.CompletedItems}
                    Story points delivered: {metrics.TotalStoryPoints}
                    Average development cycle time: {metrics.AvgDevCycleHours:F1} hours
                    Average PR review turnaround: {metrics.AvgReviewLatencyHours:F1} hours
                    Approved leave: {metrics.TotalLeaveDays:F1} days
                    Standups logged: {metrics.StandupCount}
                    Kudos received: {metrics.KudosReceived}
                    Happiness index: {(metrics.HappinessIndex > 0 ? $"{metrics.HappinessIndex} out of 10" : "not yet rated")}
                    """;
            },
            name: "get_member_delivery_metrics",
            description: "Returns delivery, capacity and wellbeing metrics for one squad member, looked up by name or part of a name."),

        AIFunctionFactory.Create(
            async (CancellationToken ct) =>
            {
                var portfolio = await retriever.BuildPortfolioContextAsync(ct);

                var completionRate = portfolio.TotalWorkItems > 0
                    ? $"{portfolio.CompletedItems / (double)portfolio.TotalWorkItems * 100:F0}%"
                    : "not measurable";

                return $"""
                    Sprints recorded: {portfolio.SprintsCount}
                    Work items: {portfolio.CompletedItems} of {portfolio.TotalWorkItems} complete ({completionRate})
                    Story points delivered in total: {portfolio.TotalStoryPoints}
                    Defects that escaped to production: {portfolio.EscapedDefects}
                    Blockers: {portfolio.ResolvedBlockers} resolved, {portfolio.ActiveBlockers} still open
                    Tech talks delivered: {portfolio.TotalTechTalks}
                    """;
            },
            name: "get_delivery_trends",
            description: "Returns cumulative delivery, quality and blocker figures across every sprint this squad has run. Use for trend or history questions.")
    ];
}
