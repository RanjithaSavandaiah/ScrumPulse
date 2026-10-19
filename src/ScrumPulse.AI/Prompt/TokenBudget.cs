namespace ScrumPulse.AI.Prompt;

using ScrumPulse.AI.Configuration;

/// <summary>
/// Estimates prompt size and keeps it inside the configured context budget. Exact counts are
/// not available, so this over-counts slightly and the failure mode is a shorter prompt rather
/// than a rejected request.
/// </summary>
public static class TokenBudget
{
    private const double ProseCharactersPerToken = 4.0;

    // Digits and punctuation split far more finely than words, and dominate a metrics prompt.
    private const double SymbolCharactersPerToken = 2.5;

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

    // Trims on a line boundary: prompts are one metric per line, and a mid-line cut would hand
    // the model a truncated number to reason from.
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
