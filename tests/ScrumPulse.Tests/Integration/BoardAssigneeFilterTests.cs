namespace ScrumPulse.Tests.Integration;

using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using Xunit;

/// <summary>
/// Covers the default that keeps a shared client project from leaking into a squad's metrics.
/// </summary>
/// <remarks>
/// The offshore squad shares an Azure DevOps project with the client's own developers and QA.
/// Importing everyone's work would do two kinds of damage: it would bury the squad's delivery
/// signal in unrelated items, making every cycle-time figure meaningless, and it would pull
/// records about people who never agreed to be tracked in this tool.
/// </remarks>
public class BoardAssigneeFilterTests
{
    [Fact]
    public void NewConnection_FiltersToRosterMembers_ByDefault()
    {
        // The safe default has to be the restrictive one. A connection that pulls everything
        // unless told otherwise would quietly over-collect on first use, which is exactly
        // when nobody is checking.
        var connection = new BoardConnection();

        Assert.Equal(BoardAssigneeFilter.RosterMembersOnly, connection.AssigneeFilter);
    }

    [Fact]
    public void NewConnection_IsEnabledButHasNoCredential()
    {
        var connection = new BoardConnection();

        Assert.True(connection.IsEnabled);
        Assert.False(connection.HasStoredCredential);
    }

    [Fact]
    public void HasStoredCredential_ReportsPresenceWithoutExposingTheToken()
    {
        // The UI needs to show "connected" without the token ever reaching the browser.
        var connection = new BoardConnection { EncryptedCredential = "protected-payload" };

        Assert.True(connection.HasStoredCredential);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HasStoredCredential_IsFalse_ForBlankValues(string? storedCredential)
    {
        var connection = new BoardConnection { EncryptedCredential = storedCredential };

        Assert.False(connection.HasStoredCredential);
    }

    [Fact]
    public void AreaPathFilter_IsOptIn()
    {
        // Pulling everything under an area path is only correct where that path already
        // isolates the squad, so it must be chosen deliberately rather than inherited.
        var connection = new BoardConnection
        {
            AssigneeFilter = BoardAssigneeFilter.EveryoneInAreaPath,
            TeamOrAreaPath = @"Contoso\Offshore"
        };

        Assert.Equal(BoardAssigneeFilter.EveryoneInAreaPath, connection.AssigneeFilter);
        Assert.NotNull(connection.TeamOrAreaPath);
    }
}
