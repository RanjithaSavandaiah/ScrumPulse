namespace ScrumPulse.AI.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ScrumPulse.AI.Configuration;
using ScrumPulse.AI.Prompt;
using ScrumPulse.AI.Providers;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Services;
using ScrumPulse.Domain.Entities;

/// <summary>
/// Splits a spoken standup into per-member draft updates.
/// </summary>
/// <remarks>
/// <para>
/// Built for the case where the Scrum Master is already on the Teams call and would otherwise
/// be typing while trying to listen. The browser transcribes, this service attributes what
/// was said to the people on the roster, and the Scrum Master reviews before anything is
/// saved.
/// </para>
/// <para>
/// Three things keep it honest. Members are matched against the actual roster rather than
/// invented, so a misheard name surfaces as an unmatched speaker instead of a wrong
/// attribution. Each draft carries a confidence score so the shakiest attributions get looked
/// at first. And nothing is written: <see cref="StandupDraftResponse"/> is a proposal that
/// the existing submit endpoint still has to accept.
/// </para>
/// </remarks>
public sealed class StandupTranscriptService : IStandupTranscriptService
{
    private static readonly JsonSerializerOptions TranscriptSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Confidence assigned to the rule-based split, which attributes nothing and simply
    /// reports that the transcript needs manual entry.
    /// </summary>
    private const double RuleBasedConfidence = 0.0;

    /// <summary>Mood used when the transcript gives no emotional signal either way.</summary>
    private const int NeutralMoodScore = 3;

    private readonly IAppDbContext _dbContext;
    private readonly IAgentProvider _agentProvider;
    private readonly DailyRequestBudget _requestBudget;
    private readonly AgentConfiguration _configuration;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<StandupTranscriptService> _logger;

    public StandupTranscriptService(
        IAppDbContext dbContext,
        IAgentProvider agentProvider,
        DailyRequestBudget requestBudget,
        AgentConfiguration configuration,
        ITenantContext tenantContext,
        ILogger<StandupTranscriptService> logger)
    {
        _dbContext = dbContext;
        _agentProvider = agentProvider;
        _requestBudget = requestBudget;
        _configuration = configuration;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<StandupDraftResponse> DraftFromTranscriptAsync(
        DraftStandupsFromTranscriptRequest request,
        CancellationToken ct = default)
    {
        // The roster comes from the tenant-filtered context, so only this squad's members are
        // candidates. It also bounds the problem: the model matches against a known list
        // rather than inventing names from a noisy transcription.
        var roster = await _dbContext.TeamMembers
            .AsNoTracking()
            .Select(member => new { member.Id, member.Name })
            .ToListAsync(ct);

        if (roster.Count == 0)
        {
            return new StandupDraftResponse([], [], WasModelGenerated: false);
        }

        if (!_agentProvider.IsLiveInferenceAvailable || !_requestBudget.TryReserve())
        {
            _logger.LogInformation(
                "Transcript drafting requested with no inference available. Returning an unattributed draft.");

            return BuildUnattributedFallback(request.Transcript!);
        }

        var rosterNames = roster.Select(member => member.Name).ToList();
        var transcript = TokenBudget.TrimToBudget(request.Transcript!, _configuration.EffectiveContextTokens);

        // The facilitator is whoever is running this request. Naming them lets the agent
        // attribute the update they give without calling their own name, which would
        // otherwise be the one update in the ceremony that gets dropped. Only used when the
        // session name matches someone on the roster, so it cannot introduce a new person.
        var facilitatorName = rosterNames.FirstOrDefault(name =>
            string.Equals(name, _tenantContext.CurrentUser, StringComparison.OrdinalIgnoreCase));

        var facilitatorGuidance = facilitatorName is null
            ? "- The facilitator is not identifiable on the roster, so leave their own update out."
            : $"""
               - The facilitator running this standup is {facilitatorName}. They will not call
                 their own name, so attribute any update that is clearly theirs to them.
               """;

        var instructions = $$"""
            You split a daily standup transcript into one update per person.

            The squad roster is exactly: {{string.Join(", ", rosterNames)}}

            How this standup is run:
            The Scrum Master facilitates by calling out one person's name, and that person then
            gives their update. So a roster name spoken on its own, or as an invitation such as
            "Priya, go ahead" or "over to you Rahul", marks the START of that person's turn.
            Everything said after it belongs to them until the next roster name is called.

            Use that structure as your primary signal for attribution. It is far more reliable
            than guessing from the content of what was said.

            Rules:
            - Attribute each update only to a name on that roster. Never invent a person.
            - Treat the facilitator's prompts, chit-chat and scheduling talk as boundaries, not
              as part of anyone's update. Do not include "go ahead" or "thanks, next" in a field.
            {{facilitatorGuidance}}
            - If a speaker cannot be matched to the roster, list their name under unmatchedSpeakers.
            - Use only what was actually said. Do not embellish or infer work nobody mentioned.
            - Leave a field as an empty string when the person did not cover it.
            - confidence is 0 to 1: how sure you are the update belongs to that person.
              Use 0.9 or above when a clear name call preceded the update. Use below 0.5 when no
              name was called, the transcription was garbled, or two people talked over a
              handover.
            - mood is 1 to 5, judged from how the person sounded, not from how much work they
              reported. Frustration, apologising for slipping, or a blocker they have raised
              before scores low. Energy and momentum score high. Use 3 when there is nothing to
              go on: a flat factual update is not evidence of a bad mood.

            Respond with JSON only:
            {
              "drafts": [
                {
                  "name": "roster name",
                  "yesterday": "what they said they completed",
                  "today": "what they said they are doing next",
                  "blockers": "impediment mentioned, or empty string",
                  "confidence": 0.0,
                  "mood": 3
                }
              ],
              "unmatchedSpeakers": ["name heard but not on the roster"]
            }
            """;

        var agentChain = _agentProvider.BuildAgentChain(instructions, "ScrumPulse standup scribe");

        foreach (var provisioned in agentChain)
        {
            try
            {
                var response = await provisioned.Agent.RunAsync(
                    $"Standup transcript:\n\n{transcript}",
                    options: new ChatClientAgentRunOptions(new ChatOptions
                    {
                        ResponseFormat = ChatResponseFormat.Json
                    }),
                    cancellationToken: ct);

                var parsed = ParseTranscriptSplit(response.Text);
                if (parsed?.Drafts is null or { Count: 0 })
                {
                    continue;
                }

                var drafts = MatchDraftsToRoster(
                    parsed,
                    roster.ToDictionary(member => member.Name, member => member.Id, StringComparer.OrdinalIgnoreCase),
                    out var unmatchedSpeakers);

                _logger.LogInformation(
                    "Drafted {DraftCount} standup update(s) from transcript via {Provider}, {UnmatchedCount} speaker(s) unmatched",
                    drafts.Count,
                    provisioned.ProviderName,
                    unmatchedSpeakers.Count);

                return new StandupDraftResponse(drafts, unmatchedSpeakers, WasModelGenerated: true);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "{Provider} failed while drafting standups from a transcript.",
                    provisioned.ProviderName);
            }
        }

        return BuildUnattributedFallback(request.Transcript!);
    }

    /// <summary>
    /// Resolves each drafted name against the roster, dropping anything that does not match.
    /// </summary>
    /// <remarks>
    /// The model is told to use roster names, but it is not trusted to have done so. An
    /// unmatched name becomes a visible unmatched speaker rather than a draft attached to
    /// nobody, which is the difference between the Scrum Master noticing a missing update and
    /// silently losing it.
    /// </remarks>
    private static List<DraftedStandupDto> MatchDraftsToRoster(
        TranscriptSplit parsed,
        Dictionary<string, Guid> rosterByName,
        out List<string> unmatchedSpeakers)
    {
        unmatchedSpeakers = [.. parsed.UnmatchedSpeakers ?? []];
        var drafts = new List<DraftedStandupDto>();

        foreach (var draft in parsed.Drafts!)
        {
            if (string.IsNullOrWhiteSpace(draft.Name))
            {
                continue;
            }

            var speakerName = draft.Name.Trim();

            if (!rosterByName.TryGetValue(speakerName, out var memberId))
            {
                if (!unmatchedSpeakers.Contains(speakerName, StringComparer.OrdinalIgnoreCase))
                {
                    unmatchedSpeakers.Add(speakerName);
                }

                continue;
            }

            drafts.Add(new DraftedStandupDto(
                memberId,
                speakerName,
                draft.Yesterday?.Trim() ?? string.Empty,
                draft.Today?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(draft.Blockers) ? null : draft.Blockers.Trim(),
                Math.Clamp(draft.Confidence, 0, 1),
                // Clamped because the mood dial is a 1-5 scale and a model asked for a number
                // will occasionally return 0 or 7. Zero would read as the lowest possible
                // wellbeing score rather than as the absent value it actually is.
                Math.Clamp(draft.Mood == 0 ? NeutralMoodScore : draft.Mood, 1, 5)));
        }

        // Least confident first: those are the ones a reviewer needs to read closely.
        return [.. drafts.OrderBy(draft => draft.Confidence)];
    }

    /// <summary>
    /// Returns the raw transcript unattributed, for when no model was available.
    /// </summary>
    /// <remarks>
    /// Deliberately does not guess at attribution with string matching. A wrong name on
    /// someone's standup is worse than no split at all, so the transcript is handed back for
    /// the Scrum Master to assign.
    /// </remarks>
    private static StandupDraftResponse BuildUnattributedFallback(string transcript) =>
        new(
            [
                new DraftedStandupDto(
                    TeamMemberId: null,
                    TeamMemberName: "Unassigned",
                    YesterdaySummary: transcript.Trim(),
                    TodayPlan: string.Empty,
                    BlockersText: null,
                    Confidence: RuleBasedConfidence,
                    MoodScore: NeutralMoodScore)
            ],
            [],
            WasModelGenerated: false);

    /// <inheritdoc />
    public async Task<AutoLoggedStandupsResponse> AutoLogFromTranscriptAsync(
        DraftStandupsFromTranscriptRequest request,
        CancellationToken ct = default)
    {
        var drafted = await DraftFromTranscriptAsync(request, ct);

        // Only attributed drafts can be written: a standup row needs a real TeamMemberId.
        // The unattributed fallback produced when no model was available has a null id, so
        // this also stops a raw transcript being filed as somebody's update.
        var attributed = drafted.Drafts.Where(draft => draft.TeamMemberId.HasValue).ToList();

        if (attributed.Count == 0)
        {
            return new AutoLoggedStandupsResponse(
                0,
                [],
                [.. drafted.UnmatchedSpeakers],
                drafted.WasModelGenerated);
        }

        var standupDate = DateTime.UtcNow.Date;
        var memberIds = attributed.Select(draft => draft.TeamMemberId!.Value).ToList();

        // Loaded in one query rather than per draft, and tracked because these get updated.
        var existingToday = await _dbContext.DailyStandups
            .Where(standup => standup.StandupDate == standupDate && memberIds.Contains(standup.TeamMemberId))
            .ToListAsync(ct);

        var auditIdentity = BuildAuditIdentity();
        var savedNames = new List<string>(attributed.Count);

        foreach (var draft in attributed)
        {
            var memberId = draft.TeamMemberId!.Value;
            var existing = existingToday.FirstOrDefault(standup => standup.TeamMemberId == memberId);

            if (existing is not null)
            {
                existing.YesterdaySummary = draft.YesterdaySummary;
                existing.TodayPlan = draft.TodayPlan;
                existing.BlockersText = draft.BlockersText;
                existing.MoodScore = draft.MoodScore;
                existing.SprintId = request.SprintId ?? existing.SprintId;
            }
            else
            {
                _dbContext.DailyStandups.Add(new DailyStandup
                {
                    TeamMemberId = memberId,
                    SprintId = request.SprintId,
                    StandupDate = standupDate,
                    YesterdaySummary = draft.YesterdaySummary,
                    TodayPlan = draft.TodayPlan,
                    BlockersText = draft.BlockersText,
                    MoodScore = draft.MoodScore,

                    // Set explicitly so the audit stamp records that this was transcribed
                    // rather than typed. The DbContext only fills CreatedBy when it is blank,
                    // so this survives the save and makes AI-logged entries identifiable.
                    CreatedBy = auditIdentity
                });
            }

            savedNames.Add(draft.TeamMemberName);
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Auto-logged {SavedCount} standup(s) from transcript; {SkippedCount} speaker(s) unmatched",
            savedNames.Count,
            drafted.UnmatchedSpeakers.Count);

        return new AutoLoggedStandupsResponse(
            savedNames.Count,
            savedNames,
            [.. drafted.UnmatchedSpeakers],
            drafted.WasModelGenerated);
    }

    /// <summary>
    /// Builds the audit identity recorded against auto-logged standups.
    /// </summary>
    /// <remarks>
    /// Names the facilitator as well as the mechanism, so the trail answers both "was this
    /// typed by the person themselves?" and "who ran the ceremony that produced it?".
    /// </remarks>
    private string BuildAuditIdentity()
    {
        var facilitator = _tenantContext.CurrentUser;

        return string.IsNullOrWhiteSpace(facilitator)
            ? "AI scribe"
            : $"AI scribe (via {facilitator})";
    }

    private TranscriptSplit? ParseTranscriptSplit(string? rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return null;
        }

        // Models asked for JSON still occasionally wrap it in a markdown fence or put a
        // sentence in front, so the outermost object is extracted rather than assuming the
        // whole reply parses.
        var start = rawResponse.IndexOf('{');
        var end = rawResponse.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TranscriptSplit>(
                rawResponse[start..(end + 1)],
                TranscriptSerializerOptions);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Transcript split response was not valid JSON.");
            return null;
        }
    }

    /// <summary>Wire shape the scribe agent is asked to return.</summary>
    private sealed class TranscriptSplit
    {
        [JsonPropertyName("drafts")]
        public List<TranscriptDraft>? Drafts { get; set; }

        [JsonPropertyName("unmatchedSpeakers")]
        public List<string>? UnmatchedSpeakers { get; set; }
    }

    /// <summary>One attributed update within a transcript split.</summary>
    private sealed class TranscriptDraft
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("yesterday")]
        public string? Yesterday { get; set; }

        [JsonPropertyName("today")]
        public string? Today { get; set; }

        [JsonPropertyName("blockers")]
        public string? Blockers { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("mood")]
        public int Mood { get; set; }
    }
}
