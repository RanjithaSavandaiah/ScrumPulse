namespace ScrumPulse.Tests.Integration;

using ScrumPulse.Application.Integration;
using Xunit;

/// <summary>
/// Covers what must not survive a sync out of a client's Azure DevOps instance.
/// </summary>
/// <remarks>
/// This is the second line of defence. The first is the metrics-only default, which imports
/// no free text at all. These tests describe what happens for teams that opt into titles
/// anyway, and they are deliberately written as "this must not appear" rather than "this is
/// formatted correctly", because the failure that matters is disclosure.
/// </remarks>
public class SensitiveContentRedactorTests
{
    [Theory]
    [InlineData("Fix login on https://client-internal.contoso.com/admin")]
    [InlineData("See www.acme-bank.co.uk/portal for the flow")]
    [InlineData("Deploy to ftp://files.client.net/release")]
    public void Redact_RemovesUrls(string text)
    {
        // An internal hostname alone discloses a client's estate, even with no path.
        var redacted = SensitiveContentRedactor.Redact(text);

        Assert.DoesNotContain("contoso.com", redacted ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("acme-bank", redacted ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("client.net", redacted ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Repro: apiKey=sk_live_abcdef1234567890abcdef")]
    [InlineData("use token: ghp_" + "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("password = Sup3rSecretValue!")]
    [InlineData("Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9")]
    public void Redact_RemovesSecrets(string text)
    {
        var redacted = SensitiveContentRedactor.Redact(text) ?? string.Empty;

        Assert.DoesNotContain("sk_live", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ghp_", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sup3rSecret", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("eyJhbGci", redacted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Redact_RemovesConnectionStrings()
    {
        const string text = "Server=prod-sql-01;Initial Catalog=ClientBilling;User Id=svc;Password=hunter2";

        var redacted = SensitiveContentRedactor.Redact(text) ?? string.Empty;

        Assert.DoesNotContain("prod-sql-01", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hunter2", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ClientBilling", redacted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Redact_RemovesEmbeddedScreenshots()
    {
        // Screenshots arrive as markup referencing an attachment. Storing the reference is as
        // much of a leak as storing the image, because the link resolves for anyone with the
        // token.
        const string text =
            @"Steps <img src=""https://dev.azure.com/org/_apis/wit/attachments/abc?fileName=customer.png"" />";

        var redacted = SensitiveContentRedactor.Redact(text) ?? string.Empty;

        Assert.DoesNotContain("attachments", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("customer.png", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", redacted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Redact_RemovesEmailAddresses()
    {
        var redacted = SensitiveContentRedactor.Redact("Raised by jane.doe@clientcorp.com") ?? string.Empty;

        Assert.DoesNotContain("jane.doe", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clientcorp", redacted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Redact_RemovesIpAddresses()
    {
        var redacted = SensitiveContentRedactor.Redact("Failing on 10.42.17.9 only") ?? string.Empty;

        Assert.DoesNotContain("10.42.17.9", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_ReturnsNull_WhenOnlySecretsRemain()
    {
        // A remnant of placeholders tells the reader nothing while implying the content was
        // benign. Better to carry no title than a misleading one.
        var redacted = SensitiveContentRedactor.Redact("https://x.contoso.com token=abcdef1234567890abcdef");

        Assert.True(string.IsNullOrWhiteSpace(redacted));
    }

    [Fact]
    public void Redact_TruncatesPastedLogs()
    {
        // A description that is really a pasted email or stack trace is both a disclosure
        // risk and useless as a title.
        var longText = string.Join(" ", Enumerable.Repeat("investigation detail", 200));

        var redacted = SensitiveContentRedactor.Redact(longText) ?? string.Empty;

        Assert.True(redacted.Length <= 201, $"Expected truncation, got {redacted.Length} characters.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public void Redact_ReturnsNull_ForEmptyInput(string? text)
    {
        Assert.Null(SensitiveContentRedactor.Redact(text));
    }

    [Fact]
    public void Redact_KeepsOrdinaryDeliveryWording()
    {
        // The redactor must not be so aggressive that nothing usable survives, or teams will
        // turn it off and import everything raw.
        var redacted = SensitiveContentRedactor.Redact("Retry failed card authorisation");

        Assert.Equal("Retry failed card authorisation", redacted);
    }
}
