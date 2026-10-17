namespace ScrumPulse.AI.Retrieval;

using ScrumPulse.AI.Strategies;

/// <summary>
/// Retrieval half of the coach: turns the squad's stored delivery data into the metric sets
/// the agents reason over.
/// </summary>
/// <remarks>
/// Kept behind an interface so the generation pipeline can be tested without a database, and
/// so the copilot's tools and the insight prompts retrieve through exactly the same code.
/// Two retrieval paths would eventually disagree, and a chat answer contradicting the
/// dashboard beside it is worse than either being slightly stale.
/// </remarks>
public interface IDeliveryMetricsRetriever
{
    /// <summary>Retrieves delivery and wellbeing metrics for one squad member.</summary>
    Task<InsightContext> BuildMemberContextAsync(Guid memberId, CancellationToken ct = default);

    /// <summary>Retrieves commitment, blocker, and morale metrics for one sprint.</summary>
    Task<InsightContext> BuildSprintContextAsync(Guid sprintId, CancellationToken ct = default);

    /// <summary>Retrieves cumulative delivery metrics across every sprint the squad has run.</summary>
    Task<InsightContext> BuildPortfolioContextAsync(CancellationToken ct = default);

    /// <summary>Retrieves the currently active sprint's metrics, or null when none is active.</summary>
    Task<InsightContext?> BuildActiveSprintContextAsync(CancellationToken ct = default);

    /// <summary>Summarises blockers still open, longest-waiting first.</summary>
    Task<IReadOnlyList<BlockerSummary>> GetOpenBlockersAsync(int maximumResults, CancellationToken ct = default);

    /// <summary>Finds a squad member by name, for resolving a name mentioned in a question.</summary>
    Task<MemberReference?> FindMemberByNameAsync(string partialName, CancellationToken ct = default);
}

/// <summary>An unresolved blocker and how long it has been waiting.</summary>
/// <param name="Title">Blocker summary as raised.</param>
/// <param name="Category">Root-cause category.</param>
/// <param name="HoursWaiting">Hours since it was raised.</param>
/// <param name="BreachesSla">Whether it has exceeded the eight hour response target.</param>
public readonly record struct BlockerSummary(string Title, string Category, double HoursWaiting, bool BreachesSla);

/// <summary>Minimal identity for a squad member the assistant has been asked about.</summary>
/// <param name="Id">Member identifier.</param>
/// <param name="Name">Member display name.</param>
public readonly record struct MemberReference(Guid Id, string Name);
