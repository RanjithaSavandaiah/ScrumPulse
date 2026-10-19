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
/// Scrum Masters only, since the input is a record of other people speaking. Audio and
/// transcripts are never written to disk or logged anywhere in this controller.
/// </remarks>
[Route("api/standup-scribe")]
[EnableRateLimiting("ai")]
[RequireScrumMaster]
public sealed class StandupScribeController(
    IStandupTranscriptService transcriptService,
    IAudioTranscriptionService audioTranscriptionService,
    ILogger<StandupScribeController> logger) : BaseApiController
{
    // Also endpoint metadata, so Kestrel rejects an oversized body before buffering it into a
    // free instance's memory.
    private const int MaxAudioUploadBytes = 20 * 1024 * 1024;

    /// <summary>
    /// Transcribes one chunk of captured meeting audio. Nothing is stored server side.
    /// </summary>
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
            // 503 not 500: the deployment is fine, it just has no provider. The client reads
            // this and falls back to pasting.
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

        // Sizes only. The audio and the transcript are a recording of colleagues speaking.
        logger.LogInformation(
            "Transcribed a {ChunkBytes} byte audio chunk into {CharacterCount} characters",
            audio.Length,
            transcript?.Length ?? 0);

        return Ok(new AudioChunkTranscriptResponse(transcript ?? string.Empty));
    }

    /// <summary>
    /// No-review path: only roster-matched updates are written, each stamped AI-drafted in its
    /// audit trail, and re-running for the same day updates rather than duplicates.
    /// </summary>
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

        // Length only, never the transcript body.
        logger.LogInformation(
            "Drafted {DraftCount} standup update(s) from a {TranscriptLength} character transcript (model generated: {WasModelGenerated})",
            result.Drafts.Count,
            request.Transcript!.Length,
            result.WasModelGenerated);

        return Ok(result);
    }
}
