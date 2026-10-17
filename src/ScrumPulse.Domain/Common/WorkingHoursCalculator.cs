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

    /// <summary>
    /// Counts the weekdays in an inclusive date range. Used for sprint capacity.
    /// </summary>
    /// <param name="startDate">First day of the range. Time of day is ignored.</param>
    /// <param name="endDate">Last day of the range, inclusive. Time of day is ignored.</param>
    /// <returns>Number of Monday-to-Friday days in the range, or zero if the range is inverted.</returns>
    /// <remarks>
    /// Sprint capacity was previously counted by a copy of this loop inlined into the AI
    /// context builder, so two places could disagree about how long a sprint was. Public
    /// holidays are still not modelled anywhere, so a squad with a holiday mid-sprint will
    /// see its capacity overstated by a day.
    /// </remarks>
    public static int CountWorkingDays(DateTime startDate, DateTime endDate)
    {
        if (endDate.Date < startDate.Date)
        {
            return 0;
        }

        var workingDays = 0;

        for (var day = startDate.Date; day <= endDate.Date; day = day.AddDays(1))
        {
            if (IsWorkingDay(day.DayOfWeek))
            {
                workingDays++;
            }
        }

        return workingDays;
    }
}
