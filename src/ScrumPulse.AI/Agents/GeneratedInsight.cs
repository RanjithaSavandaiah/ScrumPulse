namespace ScrumPulse.AI.Agents;

using System.Text.Json.Serialization;

/// <summary>
/// The shape an insight agent must return. Everything is nullable because model output is
/// untrusted however firmly the schema was requested.
/// </summary>
public sealed class GeneratedInsight
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("keyFindings")]
    public List<string>? KeyFindings { get; set; }

    [JsonPropertyName("recommendations")]
    public List<string>? Recommendations { get; set; }

    [JsonPropertyName("riskLevel")]
    public string? RiskLevel { get; set; }

    // An empty findings list honours the schema while saying nothing, so it counts as failure.
    public bool IsUsable() =>
        !string.IsNullOrWhiteSpace(Summary) &&
        KeyFindings is { Count: > 0 } &&
        Recommendations is { Count: > 0 };
}
