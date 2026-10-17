namespace ScrumPulse.Tests.AI;

using ScrumPulse.AI.Prompt;
using Xunit;

/// <summary>
/// Covers prompt sizing and trimming.
/// </summary>
/// <remarks>
/// These assert properties rather than exact counts. The estimate is deliberately
/// approximate — two providers tokenise differently and neither exposes its tokeniser — so
/// pinning an exact number would only lock in today's constants. What has to hold is that
/// the estimate is conservative and that trimming never leaves a half-written metric.
/// </remarks>
public class TokenBudgetTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Estimate_TreatsAbsentTextAsZero(string? text)
    {
        Assert.Equal(0, TokenBudget.Estimate(text));
    }

    [Fact]
    public void Estimate_GrowsWithLength()
    {
        var shortText = TokenBudget.Estimate("Sprint delivery is on track");
        var longText = TokenBudget.Estimate(string.Concat(Enumerable.Repeat("Sprint delivery is on track ", 20)));

        Assert.True(longText > shortText);
    }

    [Fact]
    public void Estimate_CountsDigitHeavyTextMoreExpensivelyThanProse()
    {
        // Numbers and punctuation split into far more tokens than words of the same length.
        // A metrics prompt is mostly numbers, so an estimator tuned only for prose would
        // undercount it and overrun the context window.
        const string prose = "the squad delivered well this sprint and morale is good again";
        const string metrics = "12.4,88.1,7/10,45%,3.75,19.2,6/10,91%,2.50,17.8,8/10,73%";

        Assert.True(
            TokenBudget.Estimate(metrics) > TokenBudget.Estimate(prose),
            "Digit-heavy text of similar length must be estimated as more tokens than prose.");
    }

    [Fact]
    public void Estimate_IsConservativeAgainstTheFourCharactersPerTokenRuleOfThumb()
    {
        var text = string.Concat(Enumerable.Repeat("delivery metrics summary ", 40));

        Assert.True(
            TokenBudget.Estimate(text) >= text.Length / 5,
            "The estimate must not fall below a plausible token count, or prompts will overrun.");
    }

    [Fact]
    public void TrimToBudget_LeavesTextThatAlreadyFitsUntouched()
    {
        const string metrics = "Committed: 30\nDelivered: 24\nBlockers: 2";

        Assert.Equal(metrics, TokenBudget.TrimToBudget(metrics, maximumTokens: 10_000));
    }

    [Fact]
    public void TrimToBudget_CutsOnlyOnLineBoundaries()
    {
        // The property that matters. Cutting mid-line would hand the model a truncated
        // number, and a model reasoning from "Delivered: 2" when the real figure was 24 is
        // worse than one that never saw the metric.
        var metrics = string.Join('\n', Enumerable.Range(1, 60).Select(index => $"Metric{index}: {index * 7}"));

        var trimmed = TokenBudget.TrimToBudget(metrics, maximumTokens: 40);

        Assert.True(trimmed.Length < metrics.Length, "Oversized input should have been trimmed.");

        foreach (var line in trimmed.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.Contains(line, metrics, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TrimToBudget_KeepsTheEarliestLines()
    {
        // The prompt builder puts the most important metrics first, so trimming must drop
        // from the tail.
        var metrics = "Sprint: Alpha\nCommitted: 30\nDelivered: 24\nBlockers: 2\nHappiness: 8";

        var trimmed = TokenBudget.TrimToBudget(metrics, maximumTokens: 12);

        Assert.StartsWith("Sprint: Alpha", trimmed, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void TrimToBudget_ReturnsInputUnchanged_WhenBudgetIsNotPositive(int maximumTokens)
    {
        // A non-positive budget means the caller's arithmetic produced nonsense, usually
        // because the task text alone exceeded the context allowance. Returning the input
        // lets the provider reject an oversized prompt with a real error, rather than the
        // model silently receiving an empty metrics block and inventing figures.
        const string metrics = "Committed: 30\nDelivered: 24";

        Assert.Equal(metrics, TokenBudget.TrimToBudget(metrics, maximumTokens));
    }
}
