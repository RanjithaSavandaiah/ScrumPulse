namespace ScrumPulse.Application.Services;

/// <summary>
/// Transcribes a chunk of meeting audio into text.
/// </summary>
/// <remarks>
/// Separate from <see cref="IStandupTranscriptService"/> on purpose. This turns sound into
/// words; that turns words into attributed updates. Keeping them apart means the drafting
/// pipeline is identical whether the transcript came from captured audio, a pasted Teams
/// transcript, or someone typing.
/// </remarks>
public interface IAudioTranscriptionService
{
    /// <summary>True when a transcription provider is configured and within its allowance.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Transcribes one audio chunk.
    /// </summary>
    /// <param name="audioStream">Audio bytes. WebM/Opus as produced by the browser recorder.</param>
    /// <param name="fileName">File name including extension, used by the provider to infer the codec.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The transcribed text, or null when transcription was unavailable or produced nothing.
    /// Null rather than an empty string so a silent chunk is distinguishable from a failure.
    /// </returns>
    Task<string?> TranscribeAsync(Stream audioStream, string fileName, CancellationToken ct = default);
}
