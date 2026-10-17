namespace ScrumPulse.Application.Services;

using ScrumPulse.Application.DTOs;

/// <summary>
/// Turns a spoken standup into per-member draft updates.
/// </summary>
/// <remarks>
/// Drafting only. Nothing this produces is persisted: the Scrum Master reviews and submits.
/// Writing a colleague's standup on their behalf, from an imperfect transcription, without
/// anyone reading it first, is not a feature anyone wants.
/// </remarks>
public interface IStandupTranscriptService
{
    /// <summary>Splits a standup transcript into per-member updates for review.</summary>
    Task<StandupDraftResponse> DraftFromTranscriptAsync(
        DraftStandupsFromTranscriptRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Splits a transcript and writes the resulting standups straight to the feed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No review step: the facilitator asked for the ceremony to log itself. Two properties
    /// keep that safe enough to be worth doing. Only updates matched to a real roster member
    /// are written, so a misheard name is reported as skipped rather than filed under the
    /// wrong person. And every row is stamped as AI-drafted in its audit trail, so anything
    /// wrong can be found and corrected later rather than being indistinguishable from what
    /// someone typed themselves.
    /// </para>
    /// <para>
    /// Re-running for the same person on the same day updates their existing entry instead of
    /// adding a second one, so a retry after a bad capture does not duplicate the feed.
    /// </para>
    /// </remarks>
    Task<AutoLoggedStandupsResponse> AutoLogFromTranscriptAsync(
        DraftStandupsFromTranscriptRequest request,
        CancellationToken ct = default);
}
