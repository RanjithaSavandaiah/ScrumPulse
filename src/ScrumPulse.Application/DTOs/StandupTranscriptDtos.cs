namespace ScrumPulse.Application.DTOs;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// A stretch of standup conversation to be split into per-member updates.
/// </summary>
/// <param name="Transcript">
/// What was said. Either live browser transcription or text pasted from a meeting tool.
/// </param>
/// <param name="SprintId">Sprint the drafted updates belong to, when one is active.</param>
public record DraftStandupsFromTranscriptRequest(
    [Required(ErrorMessage = "There is no transcript to work from yet.")]
    [StringLength(20000, MinimumLength = 40,
        ErrorMessage = "A transcript needs at least 40 characters before it can be split into updates.")]
    string? Transcript,
    Guid? SprintId);

/// <summary>
/// One member's update as drafted from the transcript, awaiting human review.
/// </summary>
/// <param name="TeamMemberId">Matched squad member, or null when the speaker could not be identified.</param>
/// <param name="TeamMemberName">Name as spoken in the transcript.</param>
/// <param name="YesterdaySummary">What the model heard them say they completed.</param>
/// <param name="TodayPlan">What the model heard them say they are picking up.</param>
/// <param name="BlockersText">Any impediment mentioned, or null.</param>
/// <param name="Confidence">
/// How confident the model is in this attribution, from 0 to 1. The UI sorts low-confidence
/// drafts to the top so the Scrum Master checks the shakiest ones first.
/// </param>
/// <param name="MoodScore">
/// Mood from 1 to 5, inferred from how the person sounded. The manual form collects this as a
/// self-reported dial, so an inferred value is explicitly a different kind of number: it is
/// recorded so the wellbeing trend has no gaps, and the entry is stamped as AI-drafted so an
/// inferred score is never mistaken for one the person gave themselves.
/// </param>
public record DraftedStandupDto(
    Guid? TeamMemberId,
    string TeamMemberName,
    string YesterdaySummary,
    string TodayPlan,
    string? BlockersText,
    double Confidence,
    int MoodScore);

/// <summary>
/// The drafting result. Nothing here has been saved.
/// </summary>
/// <param name="Drafts">Per-member updates for review.</param>
/// <param name="UnmatchedSpeakers">
/// Names heard that do not match anyone on the roster, so the Scrum Master can correct them
/// rather than silently losing the update.
/// </param>
/// <param name="WasModelGenerated">
/// False when inference was unavailable and the transcript was split by a plain rule-based
/// pass instead. Surfaced so the UI can tell the user how much to trust the split.
/// </param>
public record StandupDraftResponse(
    List<DraftedStandupDto> Drafts,
    List<string> UnmatchedSpeakers,
    bool WasModelGenerated);

/// <summary>
/// Text transcribed from one chunk of captured meeting audio.
/// </summary>
/// <param name="Text">
/// What was heard. Empty when the chunk held only silence, which is common between speakers
/// and is not treated as a failure.
/// </param>
public record AudioChunkTranscriptResponse(string Text);

/// <summary>
/// Outcome of logging a transcript straight to the standup feed with no review step.
/// </summary>
/// <param name="SavedCount">Number of standups written.</param>
/// <param name="SavedMemberNames">Who was logged, so the facilitator can see it at a glance.</param>
/// <param name="SkippedSpeakers">
/// Speakers heard but not written, because they could not be matched to anyone on the roster.
/// Reported rather than dropped silently, since an unsaved update is the one failure mode the
/// facilitator has to know about.
/// </param>
/// <param name="WasModelGenerated">False when no model was available and nothing was attributed.</param>
public record AutoLoggedStandupsResponse(
    int SavedCount,
    List<string> SavedMemberNames,
    List<string> SkippedSpeakers,
    bool WasModelGenerated);
