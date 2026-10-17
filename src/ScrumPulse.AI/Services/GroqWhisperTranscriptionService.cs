namespace ScrumPulse.AI.Services;

using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using ScrumPulse.AI.Configuration;
using ScrumPulse.AI.Providers;
using ScrumPulse.Application.Services;

/// <summary>
/// Transcribes meeting audio through Groq's Whisper endpoint.
/// </summary>
/// <remarks>
/// <para>
/// Groq exposes Whisper on the OpenAI-compatible <c>/audio/transcriptions</c> route, so this
/// is a plain multipart POST rather than anything SDK-specific. Written against
/// <see cref="HttpClient"/> directly because the chat SDK's audio surface adds no value for a
/// single multipart call and one more abstraction to keep aligned across providers.
/// </para>
/// <para>
/// The free allowance is the constraint worth designing around: roughly 20 requests a minute
/// and eight hours of audio a day. Thirty-second chunks put a standup at two requests a
/// minute, which leaves substantial room, but the shared
/// <see cref="DailyRequestBudget"/> still governs so that transcription and insight
/// generation cannot quietly exhaust each other.
/// </para>
/// </remarks>
public sealed class GroqWhisperTranscriptionService : IAudioTranscriptionService
{
    /// <summary>Named client so the host can attach timeouts and resilience policies.</summary>
    public const string HttpClientName = "groq-whisper";

    private readonly HttpClient _httpClient;
    private readonly DailyRequestBudget _requestBudget;
    private readonly AudioTranscriptionOptions _options;
    private readonly ILogger<GroqWhisperTranscriptionService> _logger;
    private readonly string? _apiKey;

    public GroqWhisperTranscriptionService(
        HttpClient httpClient,
        DailyRequestBudget requestBudget,
        AgentConfiguration agentConfiguration,
        ILogger<GroqWhisperTranscriptionService> logger)
    {
        _httpClient = httpClient;
        _requestBudget = requestBudget;
        _options = agentConfiguration.AudioTranscription;
        _logger = logger;
        _apiKey = Environment.GetEnvironmentVariable(_options.ApiKeyEnvironmentVariable);
    }

    /// <inheritdoc />
    public bool IsAvailable => !string.IsNullOrWhiteSpace(_apiKey);

    /// <inheritdoc />
    public async Task<string?> TranscribeAsync(
        Stream audioStream,
        string fileName,
        CancellationToken ct = default)
    {
        if (!IsAvailable)
        {
            _logger.LogDebug(
                "Audio transcription skipped: {Variable} is not set.",
                _options.ApiKeyEnvironmentVariable);

            return null;
        }

        if (!_requestBudget.TryReserve())
        {
            _logger.LogWarning("Audio transcription skipped: the daily AI request allowance is spent.");
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var form = new MultipartFormDataContent();
        var audioContent = new StreamContent(audioStream);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue(_options.AudioContentType);

        form.Add(audioContent, "file", fileName);
        form.Add(new StringContent(_options.Model), "model");

        // Plain text back rather than JSON with timings. Speaker labels are not something
        // Whisper provides, so the extra payload would be tokens spent on nothing: the
        // roster matching downstream works from the words alone.
        form.Add(new StringContent("text"), "response_format");

        if (!string.IsNullOrWhiteSpace(_options.Language))
        {
            // Naming the language stops Whisper guessing, which it does badly on the short,
            // jargon-dense utterances that make up a standup.
            form.Add(new StringContent(_options.Language), "language");
        }

        request.Content = form;

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode is HttpStatusCode.TooManyRequests)
            {
                _requestBudget.RecordQuotaExhausted(response.Headers.RetryAfter?.Delta);
                _logger.LogWarning("Groq rejected the transcription for quota reasons.");
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                // The provider's body can echo request detail, so only the status is logged.
                _logger.LogError(
                    "Groq transcription failed with status {StatusCode}.",
                    (int)response.StatusCode);

                return null;
            }

            var transcript = (await response.Content.ReadAsStringAsync(ct)).Trim();

            return string.IsNullOrWhiteSpace(transcript) ? null : transcript;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A dropped chunk must not end the capture. The recorder keeps going and the
            // standup loses a few seconds rather than the whole ceremony.
            _logger.LogError(exception, "Groq transcription request failed.");
            return null;
        }
    }
}
