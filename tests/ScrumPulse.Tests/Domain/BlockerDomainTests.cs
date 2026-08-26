namespace ScrumPulse.Tests.Domain;

using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using Xunit;

public class BlockerDomainTests
{
    [Fact]
    public void Blocker_Defaults_AreSetCorrectly()
    {
        var blocker = new Blocker();

        Assert.Equal(BlockerCategory.ClientClarification, blocker.Category);
        Assert.Equal(8, blocker.SlaHoursLimit);
        Assert.False(blocker.IsResolved);
        Assert.Null(blocker.ResolvedAtUtc);
    }

    [Fact]
    public void Blocker_HoursWaitingAndSlaBreach_CalculatedCorrectlyWhenOpen()
    {
        var raisedTime = DateTime.UtcNow.AddHours(-10);
        var blocker = new Blocker
        {
            RaisedAtUtc = raisedTime,
            SlaHoursLimit = 8,
            ResolvedAtUtc = null
        };

        Assert.False(blocker.IsResolved);
        Assert.True(blocker.HoursWaiting >= 9.9);
        Assert.True(blocker.IsSlaBreached);
    }

    [Fact]
    public void Blocker_WhenResolved_IsNotSlaBreachedEvenIfWaitingExceededLimit()
    {
        var raisedTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        var resolvedTime = new DateTime(2026, 8, 1, 20, 0, 0, DateTimeKind.Utc); // 11 hours

        var blocker = new Blocker
        {
            RaisedAtUtc = raisedTime,
            ResolvedAtUtc = resolvedTime,
            SlaHoursLimit = 8
        };

        Assert.True(blocker.IsResolved);
        Assert.Equal(11.0, blocker.HoursWaiting);
        Assert.False(blocker.IsSlaBreached); // Resolved blockers are no longer active SLA breaches
    }
}
