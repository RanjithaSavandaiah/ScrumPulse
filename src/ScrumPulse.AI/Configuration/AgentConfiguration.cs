namespace ScrumPulse.AI.Configuration;

/// <summary>
/// Agent behaviour. Defaults are sized for free tiers, where the constraint is a daily request
/// allowance rather than cost per token.
/// </summary>
public sealed class AgentConfiguration
{
    public string Persona { get; set; } =
        "an experienced agile delivery coach who reasons only from the metrics provided";

    public int MaxResponseTokens { get; set; } = 1024;

    public int MaxContextTokens { get; set; } = 4096;

    public float Temperature { get; set; } = 0.2f;

    // Biggest lever on free-tier quota: one model call per hour per squad, not one per view.
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromHours(1);

    public bool EnableEvaluation { get; set; } = true;

    // Under this, the deterministic analysis is served instead.
    public int MinQualityScore { get; set; } = 60;

    // Absorbs token-estimate error, so an underestimate costs prompt length, not the request.
    public double HeadroomPercentage { get; set; } = 0.15;

    public int HeadroomTokens => (int)(MaxContextTokens * HeadroomPercentage);

    public int EffectiveContextTokens => MaxContextTokens - HeadroomTokens;

    public AudioTranscriptionOptions AudioTranscription { get; set; } = new();

    // Tried in order; first with a key serves, rest are failover. Drop one by removing its key.
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

/// <summary>Speech-to-text settings, defaulted to Groq's Whisper endpoint.</summary>
public sealed class AudioTranscriptionOptions
{
    public string Endpoint { get; set; } = "https://api.groq.com/openai/v1/audio/transcriptions";

    public string Model { get; set; } = "whisper-large-v3-turbo";

    public string ApiKeyEnvironmentVariable { get; set; } = "GROQ_API_KEY";

    // Empty lets the model detect it, but naming it helps: short jargon-heavy utterances are
    // where automatic detection misfires.
    public string Language { get; set; } = "en";

    public string AudioContentType { get; set; } = "audio/webm";

    // Under Groq's 25 MB ceiling, so an oversized chunk is refused before the upload is wasted.
    public int MaxChunkBytes { get; set; } = 20 * 1024 * 1024;
}
