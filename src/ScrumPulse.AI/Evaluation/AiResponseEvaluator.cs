namespace ScrumPulse.AI.Evaluation;

using ScrumPulse.Application.DTOs;

/// <summary>
/// Evaluates AI-generated responses for quality, actionability,
/// data coverage, and risk severity calibration.
/// Returns a composite score (0-100) for observability and improvement tracking.
/// </summary>
public class AiResponseEvaluator
{
    /// <summary>
    /// Evaluates an AI suggestion response and returns a quality score.
    /// </summary>
    public EvaluationResult Evaluate(AiSuggestionResponse response)
    {
        var scores = new Dictionary<string, int>();

        // 1. Coverage: Are findings backed by data metrics?
        int coverageScore = EvaluateDataCoverage(response.KeyFindings);
        scores["DataCoverage"] = coverageScore;

        // 2. Actionability: Do recommendations contain specific, measurable actions?
        int actionabilityScore = EvaluateActionability(response.ActionableRecommendations);
        scores["Actionability"] = actionabilityScore;

        // 3. Risk Calibration: Does the risk level match the findings?
        int riskScore = EvaluateRiskCalibration(response.RiskLevel, response.KeyFindings);
        scores["RiskCalibration"] = riskScore;

        // 4. Completeness: Are all expected sections populated?
        int completenessScore = EvaluateCompleteness(response);
        scores["Completeness"] = completenessScore;

        int compositeScore = (int)scores.Values.Average();

        return new EvaluationResult(compositeScore, scores, compositeScore >= 60);
    }

    private static int EvaluateDataCoverage(IReadOnlyList<string> findings)
    {
        if (findings.Count == 0) return 0;

        int dataBackedCount = findings.Count(f =>
            f.Contains("hrs", StringComparison.OrdinalIgnoreCase) ||
            f.Contains("points", StringComparison.OrdinalIgnoreCase) ||
            f.Contains("%", StringComparison.OrdinalIgnoreCase) ||
            f.Contains("/10", StringComparison.OrdinalIgnoreCase) ||
            f.Any(char.IsDigit));

        return Math.Min(100, (int)((dataBackedCount / (double)findings.Count) * 100));
    }

    private static int EvaluateActionability(IReadOnlyList<string> recommendations)
    {
        if (recommendations.Count == 0) return 0;

        int actionableCount = recommendations.Count(r =>
            r.Contains("prioritize", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("schedule", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("enforce", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("leverage", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("continue", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("coordinate", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("encourage", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("escalate", StringComparison.OrdinalIgnoreCase) ||
            r.Length > 30); // Short recommendations tend to be vague

        return Math.Min(100, (int)((actionableCount / (double)recommendations.Count) * 100));
    }

    private static int EvaluateRiskCalibration(string riskLevel, IReadOnlyList<string> findings)
    {
        int warningCount = findings.Count(f => f.Contains("[WARNING]", StringComparison.OrdinalIgnoreCase) || f.Contains("WARNING", StringComparison.OrdinalIgnoreCase) || f.Contains("RISK", StringComparison.OrdinalIgnoreCase));
        bool hasHighRisk = riskLevel.Contains("High", StringComparison.OrdinalIgnoreCase);
        bool hasMediumRisk = riskLevel.Contains("Medium", StringComparison.OrdinalIgnoreCase);

        if (warningCount >= 2 && hasHighRisk) return 100;
        if (warningCount == 1 && hasMediumRisk) return 90;
        if (warningCount == 0 && !hasHighRisk && !hasMediumRisk) return 100;
        return 60; // Misalignment between findings and risk level
    }

    private static int EvaluateCompleteness(AiSuggestionResponse response)
    {
        int score = 0;
        if (!string.IsNullOrWhiteSpace(response.Title)) score += 20;
        if (!string.IsNullOrWhiteSpace(response.Summary)) score += 20;
        if (response.KeyFindings.Count >= 3) score += 20;
        if (response.ActionableRecommendations.Count >= 2) score += 20;
        if (!string.IsNullOrWhiteSpace(response.RiskLevel)) score += 20;
        return score;
    }
}

/// <summary>Result of AI response evaluation including composite score and dimension breakdown.</summary>
public record EvaluationResult(int CompositeScore, Dictionary<string, int> DimensionScores, bool PassesQualityGate);
