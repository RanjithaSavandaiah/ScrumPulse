namespace ScrumPulse.Application.DTOs;

using System.ComponentModel.DataAnnotations;

public record DraftStandupsFromTranscriptRequest(
    [Required(ErrorMessage = "There is no transcript to work from yet.")]
    [StringLength(20000, MinimumLength = 40,
        ErrorMessage = "A transcript needs at least 40 characters before it can be split into updates.")]
    string? Transcript,
    Guid? SprintId);

// MoodScore is inferred, and stamped as AI-drafted so it is never read as self-reported.
public record DraftedStandupDto(
    Guid? TeamMemberId,
    string TeamMemberName,
    string YesterdaySummary,
    string TodayPlan,
    string? BlockersText,
    double Confidence,
    int MoodScore);

/// <summary>The drafting result. Nothing here has been saved.</summary>
public record StandupDraftResponse(
    List<DraftedStandupDto> Drafts,
    List<string> UnmatchedSpeakers,
    bool WasModelGenerated);

/// <param name="Text">Empty when the chunk held only silence, which is not a failure.</param>
public record AudioChunkTranscriptResponse(string Text);

// Skipped speakers are reported: an unsaved update is the one failure the facilitator must see.
public record AutoLoggedStandupsResponse(
    int SavedCount,
    List<string> SavedMemberNames,
    List<string> SkippedSpeakers,
    bool WasModelGenerated);
