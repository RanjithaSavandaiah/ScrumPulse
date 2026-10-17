namespace ScrumPulse.AI.Configuration;

/// <summary>
/// Controls agent behaviour: persona, token budgets, caching, provider order, and the
/// quality bar a generated response has to clear.
/// </summary>
/// <remarks>
/// Bound from the <c>AgentConfiguration</c> configuration section. The defaults below are
/// sized for the free tiers this deployment runs on, where the binding constraint is a daily
/// request allowance rather than cost per token.
/// </remarks>
public sealed class AgentConfiguration
{
    /// <summary>Persona the agent adopts, injected as system instructions.</summary>
    public string Persona { get; set; } =
        "an experienced agile delivery coach who reasons only from the metrics provided";

    /// <summary>Upper bound on tokens the model may generate for one response.</summary>
    public int MaxResponseTokens { get; set; } = 1024;

    /// <summary>Upper bound on tokens of retrieved context sent with a prompt.</summary>
    public int MaxContextTokens { get; set; } = 4096;

    /// <summary>
    /// Sampling temperature. Held low because these answers are read as delivery analysis,
    /// and the same sprint data should not produce a different verdict on each refresh.
    /// </summary>
    public float Temperature { get; set; } = 0.2f;

    /// <summary>How long a generated insight is reused before it is regenerated.</summary>
    /// <remarks>
    /// This is the single biggest lever on free-tier quota. Sprint metrics move slowly, so an
    /// hour-old insight is still accurate, and caching turns what would be one model call per
    /// page view into roughly one per hour per squad.
    /// </remarks>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Whether to score generated responses before returning them.</summary>
    public bool EnableEvaluation { get; set; } = true;

    /// <summary>
    /// Score out of 100 a generated response must reach. Below this the deterministic
    /// analysis is served instead, so a weak generation degrades rather than ships.
    /// </summary>
    public int MinQualityScore { get; set; } = 60;

    /// <summary>Share of the context budget held back as headroom.</summary>
    /// <remarks>
    /// Token counts here are estimates: the two providers tokenise differently and neither
    /// exposes its tokeniser. This reserve absorbs that error, so an underestimate produces a
    /// slightly smaller prompt rather than a rejected request.
    /// </remarks>
    public double HeadroomPercentage { get; set; } = 0.15;

    /// <summary>Tokens reserved as headroom.</summary>
    public int HeadroomTokens => (int)(MaxContextTokens * HeadroomPercentage);

    /// <summary>Context tokens actually available to a prompt once headroom is reserved.</summary>
    public int EffectiveContextTokens => MaxContextTokens - HeadroomTokens;

    /// <summary>Settings for turning captured meeting audio into text.</summary>
    public AudioTranscriptionOptions AudioTranscription { get; set; } = new();

    /// <summary>
    /// Providers to try, in order. The first with a configured key serves; the rest are failover.
    /// </summary>
    /// <remarks>
    /// Groq leads because its free tier answers fast enough to be useful inside a standup, and
    /// Gemini follows because its daily allowance is the more generous of the two. Either can
    /// be dropped by removing its API key, with no code change.
    /// </remarks>
    public List<AiProviderOptions> Providers { get; set; } =
    [
        new AiProviderOptions
        {
            Name = "Groq",
            Endpoint = "https://api.groq.com/openai/v1",
            Model = "llama-3.3-70b-versatile",
            ApiKeyEnvironmentVariable = "GROQ_API_KEY",
            DailyRequestBudget = 900
        },
        new AiProviderOptions
        {
            Name = "Gemini",
            Endpoint = "https://generativelanguage.googleapis.com/v1beta/openai/",
            Model = "gemini-2.0-flash",
            ApiKeyEnvironmentVariable = "GEMINI_API_KEY",
            DailyRequestBudget = 1400
        }
    ];
}

/// <summary>
/// Settings for the speech-to-text pass that turns captured meeting audio into a transcript.
/// </summary>
/// <remarks>
/// Defaults target Groq's Whisper endpoint, whose free allowance is roughly twenty requests a
/// minute and eight hours of audio a day. At the default chunk length that is about two
/// requests a minute during a standup, which leaves considerable headroom.
/// </remarks>
public sealed class AudioTranscriptionOptions
{
    /// <summary>OpenAI-compatible transcription endpoint.</summary>
    public string Endpoint { get; set; } = "https://api.groq.com/openai/v1/audio/transcriptions";

    /// <summary>Transcription model. Turbo is materially faster and near-identical in quality here.</summary>
    public string Model { get; set; } = "whisper-large-v3-turbo";

    /// <summary>Environment variable holding the transcription API key.</summary>
    /// <remarks>Shares the Groq key by default, since the same account serves both endpoints.</remarks>
    public string ApiKeyEnvironmentVariable { get; set; } = "GROQ_API_KEY";

    /// <summary>
    /// Spoken language as an ISO-639-1 code, or empty to let the model detect it.
    /// </summary>
    /// <remarks>
    /// Naming the language measurably helps. Standup utterances are short and full of product
    /// jargon, which is exactly where automatic language detection misfires.
    /// </remarks>
    public string Language { get; set; } = "en";

    /// <summary>MIME type the browser recorder produces.</summary>
    public string AudioContentType { get; set; } = "audio/webm";

    /// <summary>
    /// Largest audio chunk accepted from the browser, in bytes.
    /// </summary>
    /// <remarks>
    /// Groq rejects uploads above 25 MB. The limit is set below that so an oversized chunk is
    /// refused here, with a clear message, rather than after a wasted upload.
    /// </remarks>
    public int MaxChunkBytes { get; set; } = 20 * 1024 * 1024;
}
