namespace ScrumPulse.Tests.Integration;

using ScrumPulse.Application.Integration;
using Xunit;

/// <summary>
/// Covers the extraction of coaching themes from review comments.
/// </summary>
/// <remarks>
/// Two things are being protected. The analysis has to be useful enough to justify reading
/// the comments at all, and the output must never contain a word anyone wrote - otherwise
/// client code review would end up in a hosted database by the back door.
/// </remarks>
public class ReviewCommentAnalyserTests
{
    private static ReviewComment Comment(int pullRequestId, string text) =>
        new(pullRequestId, text, IsFromRosterMember: true);

    [Fact]
    public void Analyse_NeverReturnsTheCommentText()
    {
        // The single assertion this whole design exists to satisfy.
        const string sensitive = "This breaks the Contoso billing export at https://internal.contoso.com";

        var report = ReviewCommentAnalyser.Analyse([Comment(1, sensitive)]);

        var everythingReturned = string.Join(" ", report.CoachingSignals);

        Assert.DoesNotContain("Contoso", everythingReturned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("billing", everythingReturned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal.contoso", everythingReturned, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("This is vulnerable to SQL injection", ReviewCommentTheme.Security)]
    [InlineData("Needs a null check before dereferencing", ReviewCommentTheme.ErrorHandling)]
    [InlineData("No unit test covers this branch", ReviewCommentTheme.TestCoverage)]
    [InlineData("This is an N+1 query", ReviewCommentTheme.Performance)]
    [InlineData("Duplicated from the other service, extract it", ReviewCommentTheme.DesignAndDuplication)]
    [InlineData("Typo in the variable name", ReviewCommentTheme.NamingAndReadability)]
    [InlineData("LGTM", ReviewCommentTheme.Approval)]
    public void Analyse_ClassifiesCommentsByTheme(string text, ReviewCommentTheme expected)
    {
        var report = ReviewCommentAnalyser.Analyse([Comment(1, text)]);

        Assert.Equal(expected, report.Themes[0].Theme);
    }

    [Fact]
    public void Analyse_PrioritisesSecurityOverOtherThemes()
    {
        // A comment raising both a missing test and an injection risk is a security comment.
        var report = ReviewCommentAnalyser.Analyse(
            [Comment(1, "No test for this, and it is open to injection")]);

        Assert.Equal(ReviewCommentTheme.Security, report.Themes[0].Theme);
    }

    [Fact]
    public void Analyse_TreatsQualifiedApprovalAsAChangeRequest()
    {
        // "Looks good, but..." is a change request. Reading it as an approval would flatter
        // the actionable ratio, which is the number used to spot rubber-stamping.
        var report = ReviewCommentAnalyser.Analyse(
            [Comment(1, "Looks good, but please add a null check")]);

        Assert.Equal(ReviewCommentTheme.ErrorHandling, report.Themes[0].Theme);
        Assert.Equal(1, report.ActionableComments);
    }

    [Fact]
    public void Analyse_DistinguishesOneThoroughReviewFromASquadPattern()
    {
        // Ten comments on one pull request is one reviewer being thorough. The same theme
        // across many reviews is a gap worth coaching, and only the second deserves a signal.
        var oneReview = Enumerable.Range(0, 10)
            .Select(_ => Comment(1, "Needs a unit test"))
            .ToList();

        var report = ReviewCommentAnalyser.Analyse(oneReview);

        var testTheme = report.Themes.Single(theme => theme.Theme == ReviewCommentTheme.TestCoverage);
        Assert.Equal(1, testTheme.DistinctPullRequestCount);
    }

    [Fact]
    public void Analyse_RaisesACoachingSignal_WhenAThemeSpansSeveralPullRequests()
    {
        var report = ReviewCommentAnalyser.Analyse(
            [
                Comment(1, "Missing unit test here"),
                Comment(2, "This needs test coverage"),
                Comment(3, "Please add a unit test"),
                Comment(4, "Untested path")
            ]);

        Assert.Contains(report.CoachingSignals, signal => signal.Contains("Test coverage", StringComparison.Ordinal));
        Assert.Contains(report.CoachingSignals, signal => signal.Contains("4 separate pull requests", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyse_DetectsAReviewerRepeatingThemselves()
    {
        // Near-identical wording across reviews means the point is not landing - the case the
        // user described as "repeated comment".
        var report = ReviewCommentAnalyser.Analyse(
            [
                Comment(1, "Please extract this into a shared helper"),
                Comment(2, "Please extract this into a shared helper"),
                Comment(3, "Please extract this into a shared helper")
            ]);

        var theme = report.Themes.Single(summary => summary.Theme == ReviewCommentTheme.DesignAndDuplication);

        Assert.Equal(2, theme.RecurringPhraseCount);
        Assert.Contains(report.CoachingSignals, signal => signal.Contains("word for word", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyse_TreatsCommentsDifferingOnlyByIdentifierAsTheSamePoint()
    {
        // Normalisation strips code spans, so the same guidance about two different variables
        // is recognised as one recurring point rather than two unrelated ones.
        var report = ReviewCommentAnalyser.Analyse(
            [
                Comment(1, "Rename `userDto` to something clearer"),
                Comment(2, "Rename `orderDto` to something clearer")
            ]);

        var theme = report.Themes.Single(summary => summary.Theme == ReviewCommentTheme.NamingAndReadability);

        Assert.Equal(1, theme.RecurringPhraseCount);
    }

    [Fact]
    public void Analyse_FlagsReviewsThatApproveWithoutRequestingChanges()
    {
        var rubberStamps = Enumerable.Range(1, 12)
            .Select(pullRequestId => Comment(pullRequestId, "LGTM"))
            .ToList();

        var report = ReviewCommentAnalyser.Analyse(rubberStamps);

        Assert.Contains(report.CoachingSignals, signal => signal.Contains("without being read", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyse_ComputesTheActionableRatio()
    {
        var report = ReviewCommentAnalyser.Analyse(
            [
                Comment(1, "Needs a unit test"),
                Comment(1, "LGTM"),
                Comment(2, "Why is this here?"),
                Comment(2, "Null check missing")
            ]);

        Assert.Equal(4, report.TotalComments);
        Assert.Equal(2, report.ActionableComments);
        Assert.Equal(0.5, report.ActionableRatio, precision: 2);
    }

    [Fact]
    public void Analyse_ReturnsEmpty_ForNoComments()
    {
        var report = ReviewCommentAnalyser.Analyse([]);

        Assert.Equal(0, report.TotalComments);
        Assert.Empty(report.CoachingSignals);
    }

    [Fact]
    public void Analyse_IgnoresBlankComments()
    {
        var report = ReviewCommentAnalyser.Analyse(
            [Comment(1, "   "), Comment(1, "Needs a test")]);

        Assert.Equal(1, report.TotalComments);
    }
}
