namespace ScrumPulse.Application.Integration;

using System.Text.RegularExpressions;

/// <summary>
/// Strips material that must not leave the client's Azure DevOps instance. Second line of
/// defence behind <see cref="Domain.Enums.ContentSensitivityPolicy.MetricsOnly"/>: pattern
/// matching reduces exposure and never guarantees the result is safe to publish.
/// </summary>
public static partial class SensitiveContentRedactor
{
    // Anything longer is almost certainly a pasted log or email.
    private const int MaximumRetainedLength = 200;

    private const string Placeholder = "[redacted]";

    /// <summary>Redacts a single line of text, returning null when nothing safe remains.</summary>
    public static string? Redact(string? text, int maximumRetainedLength = MaximumRetainedLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var redacted = StripHtml(text);

        // Attachments and URLs go first: a link can carry a token in its query string.
        redacted = AttachmentPattern().Replace(redacted, Placeholder);
        redacted = UrlPattern().Replace(redacted, Placeholder);
        redacted = ConnectionStringPattern().Replace(redacted, Placeholder);
        redacted = SecretAssignmentPattern().Replace(redacted, Placeholder);
        redacted = BearerTokenPattern().Replace(redacted, Placeholder);
        redacted = LongTokenPattern().Replace(redacted, Placeholder);
        redacted = EmailPattern().Replace(redacted, Placeholder);
        redacted = IpAddressPattern().Replace(redacted, Placeholder);

        redacted = WhitespacePattern().Replace(redacted, " ").Trim();

        if (redacted.Length > maximumRetainedLength)
        {
            redacted = string.Concat(redacted.AsSpan(0, maximumRetainedLength), "…");
        }

        // Mostly-placeholder text carried little but secrets, and the remnant tells the reader
        // nothing while implying the content was benign.
        return IsMostlyRedacted(redacted) ? null : redacted;
    }

    // Azure DevOps authors descriptions in HTML. An attachment reference in an img or anchor
    // tag leaks as much as the screenshot it points at.
    private static string StripHtml(string text) => HtmlTagPattern().Replace(text, " ");

    private static bool IsMostlyRedacted(string text)
    {
        if (text.Length == 0)
        {
            return true;
        }

        var placeholderCharacters = text.Split(Placeholder).Length - 1;

        return placeholderCharacters * Placeholder.Length > text.Length / 2;
    }

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagPattern();

    /// <summary>Azure DevOps attachment links, which is how screenshots are embedded.</summary>
    [GeneratedRegex(@"\S*(_apis/wit/attachments|\.png|\.jpg|\.jpeg|\.gif|\.bmp|\.pdf|\.docx|\.xlsx)\S*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AttachmentPattern();

    // Internal hostnames alone disclose a client's estate.
    [GeneratedRegex(@"\b(?:https?://|www\.|ftp://)\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"\b(Server|Data Source|Initial Catalog|User Id|Password|Pwd|AccountKey|SharedAccessSignature)\s*=\s*[^;\s]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ConnectionStringPattern();

    /// <summary>Assignments such as apiKey: abc123 or secret=xyz.</summary>
    [GeneratedRegex(@"\b(api[_-]?key|secret|token|password|passwd|credential|pat)\b\s*[:=]\s*\S+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SecretAssignmentPattern();

    [GeneratedRegex(@"\b(Bearer|Basic)\s+[A-Za-z0-9\-._~+/]{10,}=*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BearerTokenPattern();

    // The bluntest rule here. 32 characters lets ordinary words through and still occasionally
    // eats a legitimate identifier.
    [GeneratedRegex(@"\b[A-Za-z0-9+/_\-]{32,}={0,2}\b", RegexOptions.Compiled)]
    private static partial Regex LongTokenPattern();

    [GeneratedRegex(@"\b[\w.+-]+@[\w-]+\.[\w.-]+\b", RegexOptions.Compiled)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\b\d{1,3}(\.\d{1,3}){3}\b", RegexOptions.Compiled)]
    private static partial Regex IpAddressPattern();

    [GeneratedRegex(@"\s{2,}", RegexOptions.Compiled)]
    private static partial Regex WhitespacePattern();
}
