namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScrumPulse.Api.Filters;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Services;

/// <summary>
/// Drafts standup updates from what was said on the call.
/// </summary>
/// <remarks>
/// <para>
/// Restricted to Scrum Masters. The endpoint accepts a record of other people speaking, so
/// the person supplying it should be the one running the ceremony rather than anyone who can
/// reach the API.
/// </para>
/// <para>
/// Nothing here writes a standup. The response is a set of drafts the Scrum Master reviews
/// and then submits through the normal standup endpoint, which keeps one validated write
/// path rather than a second that bypasses it.
/// </para>
/// </remarks>
[Route("api/standup-scribe")]
[EnableRateLimiting("ai")]
[RequireScrumMaster]
public sealed class StandupScribeController(
    IStandupTranscriptService transcriptService,
    IAudioTranscriptionService audioTranscriptionService,
    ILogger<StandupScribeController> logger) : BaseApiController
{
    /// <summary>
    /// Largest audio upload accepted, matching the provider's own ceiling.
    /// </summary>
    /// <remarks>
    /// Enforced as endpoint metadata as well as checked below. Without the attribute, Kestrel
    /// buffers the whole body before the action runs, so an oversized post would consume the
    /// memory of a free instance before anything had the chance to reject it.
    /// </remarks>
    private const int MaxAudioUploadBytes = 20 * 1024 * 1024;

    /// <summary>Transcribes one chunk of captured meeting audio.</summary>
    /// <remarks>
    /// The browser records the meeting in short chunks and posts each one as it completes, so
    /// the transcript builds up during the ceremony instead of arriving in a single upload at
    /// the end. Chunks are transcribed and returned; nothing is stored server side.
    /// </remarks>
    [HttpPost("transcribe")]
    [RequestSizeLimit(MaxAudioUploadBytes)]
    [ProducesResponseType(typeof(AudioChunkTranscriptResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AudioChunkTranscriptResponse>> Transcribe(
        IFormFile audio,
        CancellationToken ct = default)
    {
        if (audio is null || audio.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "No audio received",
                Detail = "The request contained no audio to transcribe."
            });
        }

        if (!audioTranscriptionService.IsAvailable)
        {
            // 503 rather than 500: the deployment is working as configured, it simply has no
            // transcription provider. The client uses this to fall back to pasting.
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Transcription unavailable",
                Detail = "No speech-to-text provider is configured. Paste the meeting transcript instead."
            });
        }

        await using var audioStream = audio.OpenReadStream();

        var transcript = await audioTranscriptionService.TranscribeAsync(
            audioStream,
            audio.FileName,
            ct);

        // Audio is never written to disk and the transcript is never logged. Both are a
        // recording of colleagues speaking, and neither belongs in application storage.
        logger.LogInformation(
            "Transcribed a {ChunkBytes} byte audio chunk into {CharacterCount} characters",
            audio.Length,
            transcript?.Length ?? 0);

        return Ok(new AudioChunkTranscriptResponse(transcript ?? string.Empty));
    }

    /// <summary>Splits a transcript and logs the resulting standups straight to the feed.</summary>
    /// <remarks>
    /// The no-review path. Only updates matched to a real roster member are written, and each
    /// is stamped as AI-drafted in its audit trail so it can be found and corrected later.
    /// Re-running for the same day updates existing entries rather than duplicating them.
    /// </remarks>
    [HttpPost("auto-log")]
    [ProducesResponseType(typeof(AutoLoggedStandupsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AutoLoggedStandupsResponse>> AutoLog(
        [FromBody] DraftStandupsFromTranscriptRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await transcriptService.AutoLogFromTranscriptAsync(request, ct);

        logger.LogInformation(
            "Auto-logged {SavedCount} standup(s) from a {TranscriptLength} character transcript; {SkippedCount} speaker(s) unmatched",
            result.SavedCount,
            request.Transcript!.Length,
            result.SkippedSpeakers.Count);

        return Ok(result);
    }

    /// <summary>Splits a standup transcript into per-member draft updates for review.</summary>
    [HttpPost("draft")]
    [ProducesResponseType(typeof(StandupDraftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<StandupDraftResponse>> Draft(
        [FromBody] DraftStandupsFromTranscriptRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await transcriptService.DraftFromTranscriptAsync(request, ct);

        // Transcript contents are never logged. They carry colleagues' unreviewed words, and
        // application logs are the wrong place for that to end up.
        logger.LogInformation(
            "Drafted {DraftCount} standup update(s) from a {TranscriptLength} character transcript (model generated: {WasModelGenerated})",
            result.Drafts.Count,
            request.Transcript!.Length,
            result.WasModelGenerated);

        return Ok(result);
    }
}
