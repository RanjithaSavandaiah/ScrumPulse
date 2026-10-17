namespace ScrumPulse.AI.Prompt;

using ScrumPulse.AI.Configuration;

/// <summary>
/// Estimates prompt size and keeps it inside the configured context budget.
/// </summary>
/// <remarks>
/// <para>
/// An exact count is not achievable here. Groq's Llama models and Gemini use different
/// tokenisers, neither is exposed to the client, and the prompt has to be sized before a
/// provider is chosen. What this class provides is a deliberately conservative estimate:
/// it over-counts slightly, and <see cref="AgentConfiguration.HeadroomPercentage"/> reserves
/// further margin, so the failure mode is a marginally shorter prompt rather than a request
/// the provider rejects.
/// </para>
/// <para>
/// The ratio comes from the usual English-text observation of roughly four characters per
/// token, adjusted upward for the digits and punctuation that dominate metric-heavy prompts:
/// those tokenise far less efficiently than prose, often one token per two or three
/// characters.
/// </para>
/// </remarks>
public static class TokenBudget
{
    /// <summary>Average characters per token for ordinary prose.</summary>
    private const double ProseCharactersPerToken = 4.0;

    /// <summary>
    /// Average characters per token for digits and punctuation, which split far more finely
    /// than words and make up much of a metrics prompt.
    /// </summary>
    private const double SymbolCharactersPerToken = 2.5;

    /// <summary>
    /// Estimates the number of tokens <paramref name="text"/> is likely to occupy.
    /// </summary>
    public static int Estimate(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var symbolCharacters = 0;
        foreach (var character in text)
        {
            if (char.IsDigit(character) || (char.IsPunctuation(character) && character != '\''))
            {
                symbolCharacters++;
            }
        }

        var proseCharacters = text.Length - symbolCharacters;

        return (int)Math.Ceiling(
            (proseCharacters / ProseCharactersPerToken) +
            (symbolCharacters / SymbolCharactersPerToken));
    }

    /// <summary>
    /// Returns <paramref name="text"/> unchanged when it fits the budget, or trimmed to the
    /// last complete line that does.
    /// </summary>
    /// <remarks>
    /// Trimming on a line boundary matters: prompts here are one metric per line, and cutting
    /// mid-line would hand the model a truncated number it could then reason from. Losing a
    /// whole metric is recoverable; inventing a wrong one is not.
    /// </remarks>
    public static string TrimToBudget(string text, int maximumTokens)
    {
        if (maximumTokens <= 0 || Estimate(text) <= maximumTokens)
        {
            return text;
        }

        var lines = text.Split('\n');
        var kept = new List<string>(lines.Length);
        var runningTotal = 0;

        foreach (var line in lines)
        {
            // +1 approximates the newline that rejoining will reintroduce.
            var lineTokens = Estimate(line) + 1;

            if (runningTotal + lineTokens > maximumTokens)
            {
                break;
            }

            kept.Add(line);
            runningTotal += lineTokens;
        }

        return string.Join('\n', kept);
    }
}
