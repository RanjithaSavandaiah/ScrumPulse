namespace ScrumPulse.AI.Configuration;

/// <summary>
/// Agent harness configuration controlling AI behavior, token budgets,
/// caching, and evaluation rules. Configurable per environment.
/// </summary>
public class AgentConfiguration
{
    /// <summary>Agent persona for prompt engineering.</summary>
    public string Persona { get; set; } = "Senior Agile Coach with Microsoft Certified Scrum Master expertise";

    /// <summary>Maximum token budget for a single AI response.</summary>
    public int MaxResponseTokens { get; set; } = 2048;

    /// <summary>Maximum token budget for RAG context injection.</summary>
    public int MaxContextTokens { get; set; } = 4096;

    /// <summary>Temperature for response variability (0.0 = deterministic, 1.0 = creative).</summary>
    public double Temperature { get; set; } = 0.3;

    /// <summary>Cache TTL for generated insights (prevents re-generation of identical context).</summary>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Enable/disable response evaluation scoring.</summary>
    public bool EnableEvaluation { get; set; } = true;

    /// <summary>Minimum acceptable quality score (0-100). Responses below this trigger regeneration.</summary>
    public int MinQualityScore { get; set; } = 60;

    /// <summary>
    /// Headroom percentage: how much of the token budget to reserve for safety margin.
    /// E.g., 0.15 = reserve 15% of MaxContextTokens.
    /// </summary>
    public double HeadroomPercentage { get; set; } = 0.15;

    /// <summary>Calculated headroom in tokens.</summary>
    public int HeadroomTokens => (int)(MaxContextTokens * HeadroomPercentage);

    /// <summary>Effective context tokens after headroom reservation.</summary>
    public int EffectiveContextTokens => MaxContextTokens - HeadroomTokens;
}
