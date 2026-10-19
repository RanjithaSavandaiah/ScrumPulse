namespace ScrumPulse.Application.Integration;

using System.Text.Json;
using ScrumPulse.Domain.Enums;

/// <summary>
/// Translates a board's own state names into ScrumPulse stages.
/// </summary>
/// <remarks>
/// <para>
/// This cannot be inferred. "Code Review" means <see cref="WorkItemStatus.PrCreated"/> on one
/// board and <see cref="WorkItemStatus.PrApproved"/> on another, and Azure DevOps process
/// templates ship different state sets again. The Scrum Master maps each state once.
/// </para>
/// <para>
/// Unmapped states are reported rather than guessed. Silently defaulting an unknown column to
/// Backlog would quietly distort every cycle-time figure derived from it, and nobody would
/// know why the numbers looked wrong.
/// </para>
/// </remarks>
public sealed class BoardStateMapping
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Board state name to ScrumPulse stage, compared case-insensitively.</summary>
    private readonly Dictionary<string, WorkItemStatus> _stageByBoardState;

    private BoardStateMapping(Dictionary<string, WorkItemStatus> stageByBoardState)
    {
        _stageByBoardState = stageByBoardState;
    }

    /// <summary>
    /// A starting mapping covering the state names Azure DevOps ships by default.
    /// </summary>
    /// <remarks>
    /// Offered as a suggestion the Scrum Master confirms or edits, never applied silently.
    /// Agile, Scrum and CMMI templates each use different words, so this covers all three.
    /// </remarks>
    public static IReadOnlyDictionary<string, WorkItemStatus> AzureDevOpsDefaults { get; } =
        new Dictionary<string, WorkItemStatus>(StringComparer.OrdinalIgnoreCase)
        {
            ["New"] = WorkItemStatus.Backlog,
            ["Proposed"] = WorkItemStatus.Backlog,
            ["To Do"] = WorkItemStatus.Backlog,
            ["Approved"] = WorkItemStatus.Backlog,
            ["Open"] = WorkItemStatus.Backlog,

            ["Active"] = WorkItemStatus.InProgress,
            ["Doing"] = WorkItemStatus.InProgress,
            ["In Progress"] = WorkItemStatus.InProgress,
            ["Committed"] = WorkItemStatus.InProgress,

            ["Code Review"] = WorkItemStatus.PrCreated,
            ["In Review"] = WorkItemStatus.PrCreated,
            ["Review"] = WorkItemStatus.PrCreated,

            ["Ready to Merge"] = WorkItemStatus.PrApproved,
            ["Approved for Merge"] = WorkItemStatus.PrApproved,

            ["Merged"] = WorkItemStatus.Merged,
            ["Ready for Test"] = WorkItemStatus.Merged,

            ["Testing"] = WorkItemStatus.InQa,
            ["In QA"] = WorkItemStatus.InQa,
            ["QA"] = WorkItemStatus.InQa,
            ["Resolved"] = WorkItemStatus.InQa,

            ["Done"] = WorkItemStatus.Done,
            ["Closed"] = WorkItemStatus.Done,
            ["Completed"] = WorkItemStatus.Done
        };

    /// <summary>Reads a stored mapping, falling back to the Azure DevOps defaults.</summary>
    public static BoardStateMapping FromJson(string? mappingJson)
    {
        if (string.IsNullOrWhiteSpace(mappingJson))
        {
            return new BoardStateMapping(
                new Dictionary<string, WorkItemStatus>(AzureDevOpsDefaults, StringComparer.OrdinalIgnoreCase));
        }

        try
        {
            var stored = JsonSerializer.Deserialize<Dictionary<string, WorkItemStatus>>(
                mappingJson,
                SerializerOptions);

            return new BoardStateMapping(
                stored is null or { Count: 0 }
                    ? new Dictionary<string, WorkItemStatus>(AzureDevOpsDefaults, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, WorkItemStatus>(stored, StringComparer.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            // Corrupt configuration must not take the sync down; the defaults still produce a
            // usable import and the unmapped report tells the Scrum Master to re-save it.
            return new BoardStateMapping(
                new Dictionary<string, WorkItemStatus>(AzureDevOpsDefaults, StringComparer.OrdinalIgnoreCase));
        }
    }

    public static string ToJson(IReadOnlyDictionary<string, WorkItemStatus> mapping) =>
        JsonSerializer.Serialize(mapping, SerializerOptions);

    /// <summary>
    /// Resolves a board state, or null when it has not been mapped.
    /// </summary>
    public WorkItemStatus? Resolve(string? boardState) =>
        !string.IsNullOrWhiteSpace(boardState) && _stageByBoardState.TryGetValue(boardState, out var stage)
            ? stage
            : null;

    /// <summary>
    /// A copy, so a caller editing the result cannot change the mapping an in-flight sync is
    /// resolving against.
    /// </summary>
    public IReadOnlyDictionary<string, WorkItemStatus> ToDictionary() =>
        new Dictionary<string, WorkItemStatus>(_stageByBoardState, StringComparer.OrdinalIgnoreCase);

    /// <summary>Board states seen during a sync that no mapping covers.</summary>
    public IReadOnlyList<string> FindUnmappedStates(IEnumerable<string> observedStates) =>
        [.. observedStates
            .Where(state => !string.IsNullOrWhiteSpace(state) && !_stageByBoardState.ContainsKey(state))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
}
