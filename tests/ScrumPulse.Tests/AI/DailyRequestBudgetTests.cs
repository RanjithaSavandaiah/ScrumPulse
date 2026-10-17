namespace ScrumPulse.Tests.AI;

using Microsoft.Extensions.Logging.Abstractions;
using ScrumPulse.AI.Providers;
using Xunit;

/// <summary>
/// Covers the quota circuit breaker that keeps the coach usable after the free-tier
/// allowance runs out.
/// </summary>
public class DailyRequestBudgetTests
{
    /// <summary>
    /// Minimal controllable clock.
    /// </summary>
    /// <remarks>
    /// Written by hand rather than taking a dependency on Microsoft.Extensions.TimeProvider.Testing
    /// for one type. Advancing time is the only capability these tests need.
    /// </remarks>
    private sealed class FakeTimeProvider(DateTimeOffset startUtc) : TimeProvider
    {
        private DateTimeOffset _utcNow = startUtc;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan amount) => _utcNow = _utcNow.Add(amount);
    }

    private static readonly DateTimeOffset MidMorning = new(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);

    private static (DailyRequestBudget Budget, FakeTimeProvider Clock) CreateBudget(int dailyLimit)
    {
        var clock = new FakeTimeProvider(MidMorning);

        return (
            new DailyRequestBudget(dailyLimit, clock, NullLogger<DailyRequestBudget>.Instance),
            clock);
    }

    [Fact]
    public void TryReserve_AllowsCallsUpToTheDailyLimit()
    {
        var (budget, _) = CreateBudget(dailyLimit: 3);

        Assert.True(budget.TryReserve());
        Assert.True(budget.TryReserve());
        Assert.True(budget.TryReserve());
        Assert.Equal(3, budget.RequestsUsedToday);
    }

    [Fact]
    public void TryReserve_RefusesOnceTheDailyLimitIsSpent()
    {
        var (budget, _) = CreateBudget(dailyLimit: 2);

        budget.TryReserve();
        budget.TryReserve();

        Assert.False(budget.TryReserve());
    }

    [Fact]
    public void TryReserve_RefusesImmediately_WhenLimitIsZero()
    {
        // How a deployment with no configured provider behaves: never attempt a call.
        var (budget, _) = CreateBudget(dailyLimit: 0);

        Assert.False(budget.TryReserve());
    }

    [Fact]
    public void TryReserve_ResetsAfterUtcMidnight()
    {
        var (budget, clock) = CreateBudget(dailyLimit: 1);

        budget.TryReserve();
        Assert.False(budget.TryReserve());

        clock.Advance(TimeSpan.FromDays(1));

        Assert.True(budget.TryReserve());
        Assert.Equal(1, budget.RequestsUsedToday);
    }

    [Fact]
    public void RecordQuotaExhausted_PausesCallsEvenWithAllowanceRemaining()
    {
        // The provider's own 429 overrides the local count, which can be an undercount after
        // a process restart on a free instance.
        var (budget, _) = CreateBudget(dailyLimit: 100);

        budget.RecordQuotaExhausted();

        Assert.False(budget.TryReserve());
    }

    [Fact]
    public void RecordQuotaExhausted_ResumesOnceTheBackoffElapses()
    {
        var (budget, clock) = CreateBudget(dailyLimit: 100);

        budget.RecordQuotaExhausted(TimeSpan.FromMinutes(10));
        Assert.False(budget.TryReserve());

        clock.Advance(TimeSpan.FromMinutes(11));

        Assert.True(budget.TryReserve());
    }

    [Fact]
    public void RecordQuotaExhausted_HonoursProviderSuppliedBackoff()
    {
        var (budget, clock) = CreateBudget(dailyLimit: 100);

        budget.RecordQuotaExhausted(TimeSpan.FromMinutes(30));

        clock.Advance(TimeSpan.FromMinutes(20));
        Assert.False(budget.TryReserve());

        clock.Advance(TimeSpan.FromMinutes(11));
        Assert.True(budget.TryReserve());
    }

    [Fact]
    public void TryReserve_IsSafeUnderConcurrentCallers()
    {
        // Several dashboard panels can request insight at once. The limit must hold exactly,
        // or the deployment overspends its allowance and starts collecting 429s.
        const int dailyLimit = 50;
        var (budget, _) = CreateBudget(dailyLimit);

        var granted = 0;

        Parallel.For(0, 500, _ =>
        {
            if (budget.TryReserve())
            {
                Interlocked.Increment(ref granted);
            }
        });

        Assert.Equal(dailyLimit, granted);
    }
}
