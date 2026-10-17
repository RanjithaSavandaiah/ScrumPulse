namespace ScrumPulse.AI.Providers;

using Microsoft.Extensions.Logging;

/// <summary>
/// Counts model calls against a daily allowance and opens a circuit when the allowance,
/// or the provider itself, says no more.
/// </summary>
/// <remarks>
/// <para>
/// The free tiers this runs on cap requests per day, not spend. Once that cap is hit every
/// further call returns 429 after a full network round trip, so the page that triggered it
/// waits seconds to learn there is no answer. Tracking the count locally lets the coach skip
/// straight to its deterministic analysis instead.
/// </para>
/// <para>
/// Counts live in memory and reset when the process restarts, which on a free Render
/// instance happens whenever it idles out. That under-counts rather than over-counts, so the
/// worst case is a handful of real 429s that <see cref="RecordQuotaExhausted"/> then latches
/// on. Surviving restarts would need shared state this deployment does not have.
/// </para>
/// </remarks>
public sealed class DailyRequestBudget
{
    private readonly int _dailyLimit;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DailyRequestBudget> _logger;
    private readonly Lock _gate = new();

    private DateOnly _currentDay;
    private int _requestsToday;
    private DateTimeOffset? _quotaExhaustedUntil;

    public DailyRequestBudget(int dailyLimit, TimeProvider timeProvider, ILogger<DailyRequestBudget> logger)
    {
        _dailyLimit = dailyLimit;
        _timeProvider = timeProvider;
        _logger = logger;
        _currentDay = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
    }

    /// <summary>
    /// Reserves one request against today's allowance.
    /// </summary>
    /// <returns>False when the allowance is spent or a provider 429 is still being honoured.</returns>
    public bool TryReserve()
    {
        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            RollOverDayIfNeeded(now);

            if (_quotaExhaustedUntil is { } exhaustedUntil && now < exhaustedUntil)
            {
                return false;
            }

            if (_requestsToday >= _dailyLimit)
            {
                _logger.LogWarning(
                    "Daily AI request allowance of {DailyLimit} is spent. Serving deterministic analysis until UTC midnight.",
                    _dailyLimit);

                return false;
            }

            _requestsToday++;
            return true;
        }
    }

    /// <summary>
    /// Records that the provider rejected a call for quota reasons, pausing further attempts.
    /// </summary>
    /// <param name="retryAfter">
    /// Provider-supplied backoff when available. Defaults to fifteen minutes, which is long
    /// enough to stop hammering a limit and short enough that a per-minute limit recovers.
    /// </param>
    public void RecordQuotaExhausted(TimeSpan? retryAfter = null)
    {
        lock (_gate)
        {
            var backoff = retryAfter ?? TimeSpan.FromMinutes(15);
            _quotaExhaustedUntil = _timeProvider.GetUtcNow().Add(backoff);

            _logger.LogWarning(
                "Provider reported quota exhaustion. Pausing AI calls for {BackoffMinutes} minutes.",
                backoff.TotalMinutes);
        }
    }

    /// <summary>Requests used so far today. Exposed for the operational health endpoint.</summary>
    public int RequestsUsedToday
    {
        get
        {
            lock (_gate)
            {
                RollOverDayIfNeeded(_timeProvider.GetUtcNow());
                return _requestsToday;
            }
        }
    }

    private void RollOverDayIfNeeded(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        if (today == _currentDay)
        {
            return;
        }

        _currentDay = today;
        _requestsToday = 0;
        _quotaExhaustedUntil = null;
    }
}
