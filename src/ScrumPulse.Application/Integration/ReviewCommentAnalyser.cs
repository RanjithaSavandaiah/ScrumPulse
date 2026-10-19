namespace ScrumPulse.Application.Integration;

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>One review comment as read from the board. Never persisted.</summary>
/// <param name="PullRequestId">Pull request the comment belongs to.</param>
/// <param name="Text">Comment body. Lives only for the duration of the analysis.</param>
/// <param name="IsFromRosterMember">
/// Whether the author is on this squad's roster. Comments from the client's reviewers are
/// counted but not attributed, so the squad sees the volume without profiling outsiders.
/// </param>
public readonly record struct ReviewComment(int PullRequestId, string Text, bool IsFromRosterMember);

/// <summary>
/// Turns review comments into coaching themes without retaining what was written.
/// </summary>
/// <remarks>
/// <para>
/// The comment text enters this class, is classified, and is discarded. Only counts and
/// theme labels leave. That keeps the genuinely useful signal - which topics recur, whether
/// reviews request changes or just approve, when a reviewer is repeating themselves - while
/// the client's code review stays on the client's server.
/// </para>
/// <para>
/// Classification is deliberately deterministic rather than model-driven. Sending a client's
/// code review to a third-party inference endpoint would be a worse disclosure than storing
/// it, and a keyword taxonomy is sufficient for the question actually being asked, which is
/// "what do we keep getting pulled up on" rather than "summarise this review".
/// </para>
/// <para>
/// Recurrence is detected by hashing normalised text, so the comparison never requires
/// keeping the original. Two comments that differ only in a variable name or whitespace hash
/// alike; the hash itself is one-way and reveals nothing about the wording.
/// </para>
/// </remarks>
public static partial class ReviewCommentAnalyser
{
    /// <summary>
    /// Themes a Scrum Master can act on, with the words that signal each.
    /// </summary>
    /// <remarks>
    /// Ordered deliberately. Security outranks everything, because a comment mentioning both
    /// a missing test and an injection risk is a security comment. Approval is matched last,
    /// so "looks good, but add a null check" is read as the change request it is.
    /// </remarks>
    private static readonly (ReviewCommentTheme Theme, string[] Keywords)[] ThemeKeywords =
    [
        (ReviewCommentTheme.Security,
            ["injection", "sanitis", "sanitiz", "xss", "csrf", "auth", "permission", "secret",
             "credential", "token", "vulnerab", "escape"]),

        (ReviewCommentTheme.ErrorHandling,
            ["null", "exception", "try", "catch", "edge case", "error handling", "validate",
             "guard", "boundary", "fail"]),

        (ReviewCommentTheme.TestCoverage,
            ["test", "coverage", "unit test", "assert", "mock", "spec", "untested"]),

        (ReviewCommentTheme.Performance,
            ["performance", "n+1", "slow", "query", "index", "allocat", "memory", "cache",
             "optimis", "optimiz"]),

        (ReviewCommentTheme.DesignAndDuplication,
            ["duplicat", "extract", "refactor", "coupling", "responsibilit", "abstraction",
             "pattern", "separate"]),

        (ReviewCommentTheme.NamingAndReadability,
            ["naming", "rename", "readab", "variable name", "typo", "formatting", "convention"]),

        (ReviewCommentTheme.Documentation,
            ["comment", "document", "readme", "explain", "docstring", "xml doc"]),

        (ReviewCommentTheme.Clarification,
            ["why ", "what is", "should we", "could you clarify", "not sure", "question"]),

        (ReviewCommentTheme.Approval,
            ["lgtm", "looks good", "nice", "ship it", "approved", "great work", "👍"])
    ];

    /// <summary>Themes that represent a requested change rather than a question or approval.</summary>
    private static readonly ReviewCommentTheme[] ActionableThemes =
    [
        ReviewCommentTheme.Security,
        ReviewCommentTheme.ErrorHandling,
        ReviewCommentTheme.TestCoverage,
        ReviewCommentTheme.Performance,
        ReviewCommentTheme.DesignAndDuplication,
        ReviewCommentTheme.NamingAndReadability,
        ReviewCommentTheme.Documentation
    ];

    /// <summary>A theme on this many distinct pull requests is a squad pattern, not an opinion.</summary>
    private const int SquadPatternThreshold = 3;

    /// <summary>Below this share of actionable comments, reviews are rubber-stamping.</summary>
    private const double RubberStampRatio = 0.2;

    /// <summary>
    /// Analyses a set of comments and returns what was learned, not what was said.
    /// </summary>
    public static ReviewInsightReport Analyse(IEnumerable<ReviewComment> comments)
    {
        var commentList = comments.Where(comment => !string.IsNullOrWhiteSpace(comment.Text)).ToList();

        if (commentList.Count == 0)
        {
            return ReviewInsightReport.Empty;
        }

        var themeOccurrences = new Dictionary<ReviewCommentTheme, List<int>>();
        var themeRecurrences = new Dictionary<ReviewCommentTheme, int>();
        var seenPhraseHashes = new HashSet<string>(StringComparer.Ordinal);
        var actionableCount = 0;

        foreach (var comment in commentList)
        {
            var theme = Classify(comment.Text);

            if (!themeOccurrences.TryGetValue(theme, out var pullRequestIds))
            {
                pullRequestIds = [];
                themeOccurrences[theme] = pullRequestIds;
            }

            pullRequestIds.Add(comment.PullRequestId);

            if (ActionableThemes.Contains(theme))
            {
                actionableCount++;
            }

            // Hashing rather than storing is what makes recurrence detection safe: the hash
            // identifies a repeat without preserving a single word of the original.
            if (!seenPhraseHashes.Add(HashNormalised(comment.Text)))
            {
                themeRecurrences[theme] = themeRecurrences.GetValueOrDefault(theme) + 1;
            }
        }

        var themes = themeOccurrences
            .Select(entry => new ReviewThemeSummary(
                entry.Key,
                entry.Value.Count,
                entry.Value.Distinct().Count(),
                themeRecurrences.GetValueOrDefault(entry.Key)))
            .OrderByDescending(summary => summary.DistinctPullRequestCount)
            .ThenByDescending(summary => summary.OccurrenceCount)
            .ToList();

        return new ReviewInsightReport(
            commentList.Count,
            actionableCount,
            themes,
            BuildCoachingSignals(themes, commentList.Count, actionableCount));
    }

    /// <summary>
    /// Turns the counts into observations a Scrum Master can act on this sprint.
    /// </summary>
    /// <remarks>
    /// Written from the aggregates alone. Nothing here can quote a comment, because by this
    /// point no comment text exists.
    /// </remarks>
    private static IReadOnlyList<string> BuildCoachingSignals(
        IReadOnlyList<ReviewThemeSummary> themes,
        int totalComments,
        int actionableComments)
    {
        var signals = new List<string>();

        foreach (var theme in themes.Where(summary =>
                     summary.Theme != ReviewCommentTheme.Approval &&
                     summary.Theme != ReviewCommentTheme.Unclassified &&
                     summary.DistinctPullRequestCount >= SquadPatternThreshold))
        {
            signals.Add(
                $"{DescribeTheme(theme.Theme)} came up on {theme.DistinctPullRequestCount} separate pull " +
                "requests. A recurring theme across reviewers is usually worth a tech talk or a lint rule " +
                "rather than another round of comments.");
        }

        foreach (var theme in themes.Where(summary => summary.RecurringPhraseCount >= 2))
        {
            signals.Add(
                $"The same {DescribeTheme(theme.Theme).ToLowerInvariant()} point was repeated " +
                $"{theme.RecurringPhraseCount} times nearly word for word. When a reviewer has to restate " +
                "something, the message is usually not landing.");
        }

        if (totalComments >= 10 && (double)actionableComments / totalComments < RubberStampRatio)
        {
            signals.Add(
                "Most review comments requested no change. That can mean the code is clean, or that " +
                "reviews are being approved without being read - worth checking which.");
        }

        return signals;
    }

    /// <summary>Classifies a comment by the first theme whose keywords it matches.</summary>
    private static ReviewCommentTheme Classify(string text)
    {
        var lowered = text.ToLowerInvariant();

        foreach (var (theme, keywords) in ThemeKeywords)
        {
            if (keywords.Any(keyword => lowered.Contains(keyword, StringComparison.Ordinal)))
            {
                return theme;
            }
        }

        return ReviewCommentTheme.Unclassified;
    }

    /// <summary>
    /// Produces a one-way hash of the comment with incidental differences removed.
    /// </summary>
    /// <remarks>
    /// Code spans, punctuation, digits and casing are stripped first, so two comments that
    /// differ only in the identifier they mention are recognised as the same point. The hash
    /// is not reversible and is never shown, so recurrence can be counted without the wording
    /// being retained anywhere.
    /// </remarks>
    private static string HashNormalised(string text)
    {
        var normalised = CodeSpanPattern().Replace(text.ToLowerInvariant(), " ");
        normalised = NonLetterPattern().Replace(normalised, " ");
        normalised = WhitespacePattern().Replace(normalised, " ").Trim();

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalised)));
    }

    private static string DescribeTheme(ReviewCommentTheme theme) => theme switch
    {
        ReviewCommentTheme.Security => "Security",
        ReviewCommentTheme.ErrorHandling => "Error handling and edge cases",
        ReviewCommentTheme.TestCoverage => "Test coverage",
        ReviewCommentTheme.Performance => "Performance",
        ReviewCommentTheme.DesignAndDuplication => "Design and duplication",
        ReviewCommentTheme.NamingAndReadability => "Naming and readability",
        ReviewCommentTheme.Documentation => "Documentation",
        ReviewCommentTheme.Clarification => "Clarifying questions",
        _ => "Review feedback"
    };

    [GeneratedRegex(@"`[^`]*`|```[\s\S]*?```", RegexOptions.Compiled)]
    private static partial Regex CodeSpanPattern();

    [GeneratedRegex(@"[^a-z\s]", RegexOptions.Compiled)]
    private static partial Regex NonLetterPattern();

    [GeneratedRegex(@"\s{2,}", RegexOptions.Compiled)]
    private static partial Regex WhitespacePattern();
}
