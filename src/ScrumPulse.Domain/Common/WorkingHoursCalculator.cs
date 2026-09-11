namespace ScrumPulse.Domain.Common;

/// <summary>
/// Calculates working business hours between two timestamps, excluding overnight non-working
/// hours (e.g. 5:30 PM to 9:00 AM) and full weekend days (Saturdays and Sundays).
/// </summary>
public static class WorkingHoursCalculator
{
    public const double DefaultDailyWorkingHours = 8.5;
    public static readonly TimeSpan DefaultWorkDayStart = new(9, 0, 0); // 9:00 AM

    /// <summary>
    /// Computes elapsed working hours between startUtc and endUtc.
    /// </summary>
    public static double CalculateWorkingHours(
        DateTime startUtc,
        DateTime endUtc,
        double dailyWorkingHours = DefaultDailyWorkingHours)
    {
        if (endUtc <= startUtc) return 0.0;
        if (dailyWorkingHours <= 0) dailyWorkingHours = DefaultDailyWorkingHours;

        var workDayStart = DefaultWorkDayStart;
        var workDayEnd = workDayStart.Add(TimeSpan.FromHours(dailyWorkingHours));

        // Same calendar day
        if (startUtc.Date == endUtc.Date)
        {
            if (!IsWorkingDay(startUtc.DayOfWeek)) return 0.0;

            var startTod = startUtc.TimeOfDay;
            var endTod = endUtc.TimeOfDay;

            var effectiveStart = startTod < workDayStart ? workDayStart : (startTod > workDayEnd ? workDayEnd : startTod);
            var effectiveEnd = endTod < workDayStart ? workDayStart : (endTod > workDayEnd ? workDayEnd : endTod);

            if (effectiveEnd <= effectiveStart) return 0.0;
            return Math.Round((effectiveEnd - effectiveStart).TotalHours, 1);
        }

        // Multi-day transition
        double totalHours = 0.0;

        // 1. Working hours on start day (if weekday)
        if (IsWorkingDay(startUtc.DayOfWeek))
        {
            var startTod = startUtc.TimeOfDay;
            if (startTod < workDayEnd)
            {
                var effectiveStart = startTod < workDayStart ? workDayStart : startTod;
                totalHours += (workDayEnd - effectiveStart).TotalHours;
            }
        }

        // 2. Full business days between start and end (strictly excluding Saturdays & Sundays)
        var curDate = startUtc.Date.AddDays(1);
        while (curDate < endUtc.Date)
        {
            if (IsWorkingDay(curDate.DayOfWeek))
            {
                totalHours += dailyWorkingHours;
            }
            curDate = curDate.AddDays(1);
        }

        // 3. Working hours on end day (if weekday)
        if (IsWorkingDay(endUtc.DayOfWeek))
        {
            var endTod = endUtc.TimeOfDay;
            if (endTod > workDayStart)
            {
                var effectiveEnd = endTod > workDayEnd ? workDayEnd : endTod;
                totalHours += (effectiveEnd - workDayStart).TotalHours;
            }
        }

        return Math.Round(Math.Max(0.0, totalHours), 1);
    }

    public static bool IsWorkingDay(DayOfWeek day) =>
        day != DayOfWeek.Saturday && day != DayOfWeek.Sunday;
}
