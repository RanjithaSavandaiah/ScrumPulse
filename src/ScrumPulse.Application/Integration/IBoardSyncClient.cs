namespace ScrumPulse.Application.Integration;

using ScrumPulse.Domain.Entities;

/// <summary>
/// Reads work items and their state history from an external delivery board.
/// </summary>
/// <remarks>
/// Read-only by design: there is no write method and none should be added. The board stays
/// the system of record, the team's workflow is untouched, and a defect in this code cannot
/// damage a real backlog.
/// </remarks>
public interface IBoardSyncClient
{
    /// <summary>
    /// Confirms the stored credential works before a sync is attempted.
    /// </summary>
    /// <returns>Null when the connection succeeds, otherwise a message safe to show the user.</returns>
    Task<string?> TestConnectionAsync(BoardConnection connection, CancellationToken ct = default);

    /// <summary>
    /// Fetches work items with the stage timestamps derived from the board's own history.
    /// </summary>
    /// <param name="connection">Squad's board configuration, including the state mapping.</param>
    /// <param name="changedSince">
    /// When supplied, limits the query to items touched since then. Incremental syncs matter
    /// on a free tier: a full backlog pull on every dashboard load would exhaust both the
    /// board's rate limit and the instance's patience.
    /// </param>
    /// <param name="rosterIdentities">
    /// Board identities of the squad's members, normally their email addresses. When the
    /// connection filters by roster, only items assigned to these people are fetched, so a
    /// shared client project yields just this squad's work.
    /// </param>
    Task<IReadOnlyList<ImportedWorkItem>> FetchWorkItemsAsync(
        BoardConnection connection,
        IReadOnlyCollection<string> rosterIdentities,
        DateTime? changedSince,
        CancellationToken ct = default);
}
