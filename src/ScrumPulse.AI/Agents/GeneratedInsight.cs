namespace ScrumPulse.AI.Agents;

using System.Text.Json.Serialization;

/// <summary>
/// The shape an insight agent is required to return.
/// </summary>
/// <remarks>
/// <para>
/// Asking for structured output rather than prose is what makes the result usable. The UI
/// renders findings and recommendations as separate lists, and the evaluator scores them
/// independently, so free text would have to be parsed back apart with heuristics that break
/// the moment the model changes its phrasing.
/// </para>
/// <para>
/// Every property is nullable because this is deserialised from model output, which is not
/// a trusted source however firmly the schema was requested. Validation happens after
/// deserialisation rather than being assumed here.
/// </para>
/// </remarks>
public sealed class GeneratedInsight
{
    /// <summary>Short headline for the analysis.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>Two or three sentences stating the overall position.</summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    /// <summary>Observations, each citing a figure from the supplied metrics.</summary>
    [JsonPropertyName("keyFindings")]
    public List<string>? KeyFindings { get; set; }

    /// <summary>Specific actions a Scrum Master could take this sprint.</summary>
    [JsonPropertyName("recommendations")]
    public List<string>? Recommendations { get; set; }

    /// <summary>One of Low, Medium, High, or Critical.</summary>
    [JsonPropertyName("riskLevel")]
    public string? RiskLevel { get; set; }

    /// <summary>
    /// True when the response carries enough of each section to be worth showing.
    /// </summary>
    /// <remarks>
    /// A model that returns an empty findings list has technically honoured the schema while
    /// saying nothing. Treating that as a failure sends the caller to the deterministic
    /// analysis, which at least states the numbers.
    /// </remarks>
    public bool IsUsable() =>
        !string.IsNullOrWhiteSpace(Summary) &&
        KeyFindings is { Count: > 0 } &&
        Recommendations is { Count: > 0 };
}
