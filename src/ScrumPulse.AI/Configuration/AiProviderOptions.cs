namespace ScrumPulse.AI.Configuration;

/// <summary>
/// One inference provider the coach can talk to.
/// </summary>
/// <remarks>
/// API keys are never held here. They are read from the environment variable named by
/// <see cref="ApiKeyEnvironmentVariable"/> at the moment a client is built, so a key cannot
/// reach appsettings.json and from there a public repository.
/// </remarks>
public sealed class AiProviderOptions
{
    /// <summary>Name used in logs and health output. Not a secret.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>OpenAI-compatible base address, including the trailing path segment.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Model identifier as the provider names it.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Environment variable holding this provider's API key.</summary>
    public string ApiKeyEnvironmentVariable { get; set; } = string.Empty;

    /// <summary>
    /// Requests allowed per day on the current plan. Used by the local budget guard so the
    /// app stops before the provider starts rejecting, rather than after.
    /// </summary>
    public int DailyRequestBudget { get; set; }

    /// <summary>True when the key for this provider is present in the environment.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKeyEnvironmentVariable) &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable));

    /// <summary>Reads the API key from the environment, or null when it is not set.</summary>
    public string? ReadApiKey() =>
        string.IsNullOrWhiteSpace(ApiKeyEnvironmentVariable)
            ? null
            : Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);
}
