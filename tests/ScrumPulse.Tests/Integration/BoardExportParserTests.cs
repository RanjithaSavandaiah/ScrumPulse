namespace ScrumPulse.Tests.Integration;

using ScrumPulse.Application.Integration;
using ScrumPulse.Domain.Enums;
using Xunit;

/// <summary>
/// Covers reading an Azure DevOps CSV export into importable work items.
/// </summary>
/// <remarks>
/// <para>
/// This is the route for squads whose client will not issue a personal access token, so it is
/// the route most imports actually take. It also runs over a file a user chose to upload,
/// which makes it the widest input surface in the feature.
/// </para>
/// <para>
/// The quoting tests carry the most weight. A naive comma split on a real export shifts every
/// column by one from the first title that contains a comma onwards, and nothing throws: the
/// states become assignees, the points become dates, and the resulting dashboard looks
/// entirely plausible. The remaining tests pin the conversions where a wrong assumption is
/// equally quiet - an inverted priority scale, a blank estimate read as a default, a local
/// time stored as if it were UTC.
/// </para>
/// </remarks>
public class BoardExportParserTests
{
    private static BoardStateMapping DefaultMapping() => BoardStateMapping.FromJson(null);

    private static BoardExportParseResult Parse(
        string csvContent,
        ContentSensitivityPolicy contentPolicy = ContentSensitivityPolicy.RedactedTitles) =>
        BoardExportParser.Parse(csvContent, DefaultMapping(), contentPolicy);

    [Fact]
    public void Parse_KeepsColumnsAligned_WhenAQuotedFieldContainsACommma()
    {
        // The failure this prevents is silent. Split on commas and the State column receives
        // the tail of the title, so every item lands in Backlog and every stage figure reads
        // as zero, with a file that opened perfectly well in Excel.
        var result = Parse(
            """
            ID,Title,State,Story Points
            4182,"Fix totals for Contoso, Fabrikam and Northwind",Active,5
            """);

        var item = Assert.Single(result.Items);
        Assert.Equal("Fix totals for Contoso, Fabrikam and Northwind", item.Title);
        Assert.Equal(WorkItemStatus.InProgress, item.Status);
        Assert.Equal(5, item.StoryPoints);
    }

    [Fact]
    public void Parse_TreatsANewlineInsideQuotesAsPartOfTheField()
    {
        // A description or a multi-line title ends the row early under a line-based reader,
        // which turns one item into two malformed ones and loses the real columns of both.
        var result = Parse(
            "ID,Title,State\n" +
            "4182,\"Checkout fails\nwhen the basket is empty\",Active\n" +
            "4183,Second item,Closed\n");

        Assert.Equal(2, result.Items.Count);
        Assert.Equal("4182", result.Items[0].ExternalId);
        Assert.Equal("4183", result.Items[1].ExternalId);
        Assert.Equal(WorkItemStatus.Done, result.Items[1].Status);
    }

    [Fact]
    public void Parse_ReadsADoubledQuoteAsASingleCharacter()
    {
        var result = Parse(
            """
            ID,Title,State
            4182,"The ""Apply"" button does nothing",Active
            """);

        var item = Assert.Single(result.Items);
        Assert.Equal("The \"Apply\" button does nothing", item.Title);
    }

    [Fact]
    public void Parse_FindsTheIdColumn_DespiteAByteOrderMark()
    {
        // Excel writes a BOM on every round trip. Left in place it becomes part of the first
        // header, the ID column never matches, and the user is told their export has no IDs
        // when it plainly does.
        var result = Parse("﻿ID,Title,State\n4182,Totals,Active\n");

        Assert.Single(result.Items);
        Assert.Equal("4182", result.Items[0].ExternalId);
    }

    [Fact]
    public void Parse_ReturnsNothingAndExplainsWhy_WhenThereIsNoIdColumn()
    {
        // Without an ID a second import cannot match the first, so it would duplicate every
        // item and double the squad's apparent delivery. Refusing is the only safe answer,
        // and the warning has to say what to re-export.
        var result = Parse("Title,State\nTotals,Active\n");

        Assert.Empty(result.Items);
        Assert.Contains(result.Warnings, warning => warning.Contains("ID column", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_MatchesHeadersCaseInsensitivelyAndUnderAlternativeNames()
    {
        // The header set depends on which fields the person included in their query and on
        // their organisation's process template. An exact-match requirement would fail most
        // real exports on the first attempt.
        var result = Parse(
            """
            work item id,TITLE,State,Assigned To,Iteration Path,Effort
            4182,Totals,Active,Priya Raman,Contoso\Sprint 12,8
            """);

        var item = Assert.Single(result.Items);
        Assert.Equal("4182", item.ExternalId);
        Assert.Equal("Totals", item.Title);
        Assert.Equal("Priya Raman", item.AssignedToDisplayName);
        Assert.Equal(@"Contoso\Sprint 12", item.IterationPath);
        Assert.Equal(8, item.StoryPoints);
    }

    [Fact]
    public void Parse_ImportsARepeatedWorkItemOnlyOnce()
    {
        // A query joining parents to children repeats the parent on every child row. Importing
        // it once per child multiplies its story points across the sprint and inflates velocity
        // by however many children it happens to have.
        var result = Parse(
            """
            ID,Title,State,Story Points
            4182,Parent epic,Active,8
            4183,First child,Active,3
            4182,Parent epic,Active,8
            4184,Second child,Active,2
            """);

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(13, result.Items.Sum(item => item.StoryPoints));
    }

    [Fact]
    public void Parse_SkipsBlankTrailingRowsWithoutComplaining()
    {
        // Excel leaves these behind on every save. Warning about them would train the user to
        // ignore the warning list, which is where the real problems are reported.
        var result = Parse("ID,Title,State\n4182,Totals,Active\n\n,,\n");

        Assert.Single(result.Items);
        Assert.DoesNotContain(result.Warnings, warning => warning.Contains("had no work item ID", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_WarnsAboutARowThatCarriedContentButNoId()
    {
        // Unlike a blank line this is a real row that will not be imported, so the squad needs
        // to know its figures are short by one rather than discovering it later.
        var result = Parse("ID,Title,State\n4182,Totals,Active\n,Orphaned row,Active\n");

        Assert.Single(result.Items);
        Assert.Contains(result.Warnings, warning => warning.Contains("no work item ID", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_ReducesAnExportedIdentityToItsDisplayName()
    {
        // Azure DevOps exports "Display Name <email>". Roster matching works on the name, and
        // a client domain's address list is personal data this deployment has no reason to hold.
        var result = Parse(
            """
            ID,Title,State,Assigned To
            4182,Totals,Active,Priya Raman <priya.raman@contoso.com>
            """);

        var item = Assert.Single(result.Items);
        Assert.Equal("Priya Raman", item.AssignedToDisplayName);
        Assert.DoesNotContain("@", item.AssignedToDisplayName!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1", PriorityLevel.Critical)]
    [InlineData("2", PriorityLevel.High)]
    [InlineData("3", PriorityLevel.Medium)]
    [InlineData("4", PriorityLevel.Low)]
    public void Parse_InvertsTheAzureDevOpsPriorityScale(string exportedPriority, PriorityLevel expected)
    {
        // Azure DevOps counts 1 as most urgent and ScrumPulse counts it as least. Carrying the
        // number across unchanged shows every critical defect as low priority on the board.
        var result = Parse($"ID,Title,State,Priority\n4182,Totals,Active,{exportedPriority}\n");

        Assert.Equal(expected, Assert.Single(result.Items).Priority);
    }

    [Fact]
    public void Parse_ReadsStoryPointsExportedAsADecimal()
    {
        // The field is a double on the board and exports as "5.0" even when whole.
        var result = Parse("ID,Title,State,Story Points\n4182,Totals,Active,5.0\n");

        Assert.Equal(5, Assert.Single(result.Items).StoryPoints);
    }

    [Fact]
    public void Parse_ReadsAnUnestimatedItemAsZeroRatherThanADefault()
    {
        // The entity defaults StoryPoints to 3. Letting that default stand would invent
        // velocity for items nobody has estimated and hide the fact that nobody has.
        var result = Parse("ID,Title,State,Story Points\n4182,Totals,Active,\n");

        Assert.Equal(0, Assert.Single(result.Items).StoryPoints);
    }

    [Fact]
    public void Parse_ReadsExportedDatesAsUtc()
    {
        // The export follows the locale of whoever downloaded it. A local time stored as if it
        // were UTC produces cycle times wrong by the offset, which look merely odd rather than
        // wrong and so are never questioned.
        var result = Parse(
            """
            ID,Title,State,Created Date,Activated Date,Closed Date
            4182,Totals,Closed,2026-09-14T08:30:00Z,2026-09-15T09:00:00Z,2026-09-20T17:00:00Z
            """);

        var item = Assert.Single(result.Items);
        Assert.Equal(new DateTime(2026, 9, 14, 8, 30, 0, DateTimeKind.Utc), item.CreatedAtUtc);
        Assert.Equal(new DateTime(2026, 9, 15, 9, 0, 0, DateTimeKind.Utc), item.Timeline.PickedUpAtUtc);
        Assert.Equal(new DateTime(2026, 9, 20, 17, 0, 0, DateTimeKind.Utc), item.Timeline.CompletedAtUtc);
        Assert.Equal(DateTimeKind.Utc, item.CreatedAtUtc.Kind);
    }

    [Fact]
    public void Parse_UnderMetricsOnly_UsesTheReferenceAndNeverTheExportedTitle()
    {
        // Applied here as well as in the merger so the client's text is dropped before it is
        // ever held in a collection, rather than travelling further in and being dropped later.
        var result = Parse(
            "ID,Title,State\n4182,Contoso renewal blocked by billing,Active\n",
            ContentSensitivityPolicy.MetricsOnly);

        var item = Assert.Single(result.Items);
        Assert.Equal("AB#4182", item.Title);
        Assert.DoesNotContain("Contoso", item.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ContentSensitivityPolicy.MetricsOnly)]
    [InlineData(ContentSensitivityPolicy.RedactedTitles)]
    [InlineData(ContentSensitivityPolicy.RedactedTitlesAndDescriptions)]
    public void Parse_NeverReadsADescriptionFromAnExport(ContentSensitivityPolicy contentPolicy)
    {
        // Descriptions are the single most likely column to carry stack traces, customer names
        // and embedded image links, and no delivery figure depends on them. Unlike the title
        // there is no policy that turns this on.
        var result = Parse(
            """
            ID,Title,State,Description
            4182,Totals,Active,"Customer ACME reported this; see https://internal/screenshot.png"
            """,
            contentPolicy);

        Assert.Equal(string.Empty, Assert.Single(result.Items).Description);
    }

    [Fact]
    public void Parse_NeverConstructsAnExternalUrl()
    {
        // An export has no reliable per-item URL, and building one would publish the client's
        // organisation and project names into a hosted database.
        var result = Parse("ID,Title,State\n4182,Totals,Active\n");

        Assert.Null(Assert.Single(result.Items).ExternalUrl);
    }

    [Fact]
    public void Parse_AlwaysStatesThatStageTimestampsCouldNotBeDerived()
    {
        // The honesty clause. Without it a file import looks identical to a measured sync on
        // the dashboard, and nobody can tell afterwards which figures were observed.
        var result = Parse("ID,Title,State\n4182,Totals,Active\n");

        Assert.Contains(
            result.Warnings,
            warning => warning.Contains("no history", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_NamesTheBoardStatesNoMappingCovered()
    {
        // Defaulting an unknown column to Backlog quietly zeroes every stage figure derived
        // from it, so the column has to be reported by name for the mapping to be corrected.
        var result = Parse(
            """
            ID,Title,State
            4182,Totals,Awaiting Client Sign-off
            4183,Shipping,Active
            """);

        Assert.Contains(
            result.Warnings,
            warning => warning.Contains("Awaiting Client Sign-off", StringComparison.Ordinal));

        Assert.Equal(WorkItemStatus.Backlog, result.Items[0].Status);
    }

    [Fact]
    public void Parse_ReportsAnEmptyFileRatherThanSucceedingSilently()
    {
        var result = Parse("   ");

        Assert.Empty(result.Items);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Parse_ReportsHeaderOnlyFileAsContainingNoRows()
    {
        var result = Parse("ID,Title,State\n");

        Assert.Empty(result.Items);
        Assert.Contains(result.Warnings, w => w.Contains("contained no rows"));
    }
}
