namespace ScrumPulse.Application.Integration;

/// <summary>
/// What a review comment was about, without recording what it said. Derived in memory, and only
/// the category survives, so the client's code review stays on their server.
/// </summary>
public enum ReviewCommentTheme
{
    /// <summary>Did not match a known theme. Counted, never quoted.</summary>
    Unclassified = 0,

    NamingAndReadability = 1,

    TestCoverage = 2,

    ErrorHandling = 3,

    Performance = 4,

    Security = 5,

    DesignAndDuplication = 6,

    Documentation = 7,

    /// <summary>A question rather than a change request.</summary>
    Clarification = 8,

    Approval = 9
}

/// <param name="DistinctPullRequestCount">
/// Ten hits on one review is a thorough reviewer; eight reviews is a squad-wide gap.
/// </param>
public readonly record struct ReviewThemeSummary(
    ReviewCommentTheme Theme,
    int OccurrenceCount,
    int DistinctPullRequestCount,
    int RecurringPhraseCount);

/// <summary>CoachingSignals come from the counts, never from the comment text.</summary>
public sealed record ReviewInsightReport(
    int TotalComments,
    int ActionableComments,
    IReadOnlyList<ReviewThemeSummary> Themes,
    IReadOnlyList<string> CoachingSignals)
{
    public static ReviewInsightReport Empty { get; } = new(0, 0, [], []);

    // The useful signal: a review consisting entirely of "LGTM" is not a review.
    public double ActionableRatio => TotalComments == 0 ? 0 : (double)ActionableComments / TotalComments;
}
