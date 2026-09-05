export interface SelectOption<T = string> {
  value: T;
  label: string;
}

export interface DateRangePreset {
  startDate: string;
  endDate: string;
}

/**
 * Dynamically generates a rolling list of months (e.g. past N months + upcoming N months)
 * based on the current live runtime date.
 */
export function generateDynamicMonths(pastCount = 11, futureCount = 3, includeAllOption = false): SelectOption<string>[] {
  const options: SelectOption<string>[] = [];
  
  if (includeAllOption) {
    options.push({ value: 'ALL', label: 'All Months' });
  }

  const now = new Date();
  const currentYear = now.getFullYear();
  const currentMonth = now.getMonth(); // 0-indexed

  for (let i = futureCount; i >= -pastCount; i--) {
    const d = new Date(currentYear, currentMonth + i, 1);
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const value = `${y}-${m}`;
    
    const monthName = d.toLocaleString('default', { month: 'long' });
    const isCurrent = i === 0;
    const label = `${monthName} ${y}${isCurrent ? ' (Current)' : ''}`;

    options.push({ value, label });
  }

  return options;
}

/**
 * Dynamically generates all 12 calendar months (January to December) for a given year.
 */
export function generateCalendarYearMonths(year: number): SelectOption<string>[] {
  const options: SelectOption<string>[] = [];
  const now = new Date();
  const currentYear = now.getFullYear();
  const currentMonth = now.getMonth();

  for (let m = 0; m < 12; m++) {
    const d = new Date(year, m, 1);
    const monthName = d.toLocaleString('default', { month: 'long' });
    const value = `${year}-${String(m + 1).padStart(2, '0')}`;
    const isCurrent = year === currentYear && m === currentMonth;
    options.push({
      value,
      label: `${monthName} ${year}${isCurrent ? ' (Current)' : ''}`
    });
  }
  return options;
}

/**
 * Gets the calendar year date range strictly from January 1 to December 31.
 * Leaves and annual capacity follow the calendar year (Jan to Dec), not financial year.
 */
export function getCalendarYearRange(year: number): DateRangePreset {
  return {
    startDate: `${year}-01-01`,
    endDate: `${year}-12-31`
  };
}

/**
 * Formats a Date or ISO string into a normalized YYYY-MM-DD string for exact date comparisons.
 */
export function toDateOnlyString(val: string | Date | null | undefined): string | null {
  if (!val) return null;
  if (typeof val === 'string') {
    if (/^\d{4}-\d{2}-\d{2}/.test(val)) {
      return val.substring(0, 10);
    }
  }
  const d = new Date(val);
  if (isNaN(d.getTime())) return null;
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

/**
 * Checks whether a leave interval overlaps with a requested period.
 * Supports:
 *  - 'ALL': Matches any leave.
 *  - 'YEAR_<YYYY>': Calendar Year (strictly January 1 to December 31 of <YYYY>).
 *  - '<YYYY>-<MM>': Specific calendar month (1st to last day of that month).
 *  - 'CUSTOM': User-specified date range [customStart, customEnd].
 */
export function isLeaveInPeriod(
  leave: { startDate?: string | Date | null; endDate?: string | Date | null },
  period: string,
  customStart?: string | null,
  customEnd?: string | null
): boolean {
  if (!period || period === 'ALL') return true;
  if (!leave || !leave.startDate) return false;

  const leaveStart = toDateOnlyString(leave.startDate);
  const leaveEnd = toDateOnlyString(leave.endDate || leave.startDate);
  if (!leaveStart || !leaveEnd) return false;

  let rangeStart = '';
  let rangeEnd = '';

  if (period.startsWith('YEAR_')) {
    const yearStr = period.replace('YEAR_', '');
    const year = parseInt(yearStr, 10);
    if (isNaN(year)) return true;
    // Strict calendar year: Jan 1 to Dec 31
    rangeStart = `${year}-01-01`;
    rangeEnd = `${year}-12-31`;
  } else if (period === 'CUSTOM') {
    rangeStart = customStart ? toDateOnlyString(customStart) || '' : '';
    rangeEnd = customEnd ? toDateOnlyString(customEnd) || '' : '';

    if (!rangeStart && !rangeEnd) return true;
    if (rangeStart && !rangeEnd) return leaveEnd >= rangeStart;
    if (!rangeStart && rangeEnd) return leaveStart <= rangeEnd;
  } else if (/^\d{4}-\d{2}$/.test(period)) {
    const [yearStr, monthStr] = period.split('-');
    const year = parseInt(yearStr, 10);
    const month = parseInt(monthStr, 10);
    rangeStart = `${period}-01`;
    const lastDay = new Date(year, month, 0).getDate();
    rangeEnd = `${period}-${String(lastDay).padStart(2, '0')}`;
  } else {
    return true;
  }

  // Two intervals [leaveStart, leaveEnd] and [rangeStart, rangeEnd] overlap if:
  // leaveStart <= rangeEnd AND leaveEnd >= rangeStart
  return leaveStart <= rangeEnd && leaveEnd >= rangeStart;
}

/**
 * Dynamically generates rolling quarters based on the current runtime date.
 */
export function generateDynamicQuarters(pastCount = 7, futureCount = 1): SelectOption<string>[] {
  const options: SelectOption<string>[] = [];
  const now = new Date();
  const currentYear = now.getFullYear();
  const currentQuarter = Math.floor(now.getMonth() / 3) + 1; // 1-4

  const quarterMonths: Record<number, string> = {
    1: 'Jan - Mar',
    2: 'Apr - Jun',
    3: 'Jul - Sep',
    4: 'Oct - Dec'
  };

  for (let offset = futureCount; offset >= -pastCount; offset--) {
    let q = currentQuarter + offset;
    let y = currentYear;

    while (q < 1) {
      q += 4;
      y -= 1;
    }
    while (q > 4) {
      q -= 4;
      y += 1;
    }

    const value = `${y}-Q${q}`;
    const isCurrent = offset === 0;
    const label = `Q${q} ${y} (${quarterMonths[q]})${isCurrent ? ' (Current)' : ''}`;

    options.push({ value, label });
  }

  return options;
}

/**
 * Get current month value (YYYY-MM) dynamically.
 */
export function getCurrentMonthValue(): string {
  const now = new Date();
  const y = now.getFullYear();
  const m = String(now.getMonth() + 1).padStart(2, '0');
  return `${y}-${m}`;
}

/**
 * Get current quarter value (YYYY-Q#) dynamically.
 */
export function getCurrentQuarterValue(): string {
  const now = new Date();
  const y = now.getFullYear();
  const q = Math.floor(now.getMonth() / 3) + 1;
  return `${y}-Q${q}`;
}

/**
 * Calculates date range for a rolling N days preset (e.g. 7d, 14d, 30d).
 */
export function getDatePresetRange(days: number): DateRangePreset {
  const end = new Date();
  const start = new Date(Date.now() - days * 24 * 60 * 60 * 1000);
  const endY = end.getFullYear();
  const endM = String(end.getMonth() + 1).padStart(2, '0');
  const endD = String(end.getDate()).padStart(2, '0');
  const startY = start.getFullYear();
  const startM = String(start.getMonth() + 1).padStart(2, '0');
  const startD = String(start.getDate()).padStart(2, '0');
  return {
    startDate: `${startY}-${startM}-${startD}`,
    endDate: `${endY}-${endM}-${endD}`
  };
}

/**
 * Calculates date range for "This Month" (from 1st of month to today).
 */
export function getThisMonthDateRange(): DateRangePreset {
  const now = new Date();
  const year = now.getFullYear();
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  return {
    startDate: `${year}-${month}-01`,
    endDate: `${year}-${month}-${day}`
  };
}

/**
 * Formats a sprint's start/end dates into ISO date strings.
 */
export function getSprintDateRange(startDate?: string | Date | null, endDate?: string | Date | null): DateRangePreset | null {
  if (!startDate || !endDate) return null;
  return {
    startDate: new Date(startDate).toISOString().split('T')[0],
    endDate: new Date(endDate).toISOString().split('T')[0]
  };
}

/**
 * Calculates exact business / working days between startDate and endDate (inclusive),
 * strictly excluding Saturdays and Sundays.
 */
export function calculateWorkingDays(startDate?: string | Date | null, endDate?: string | Date | null): number {
  if (!startDate || !endDate) return 10;
  const start = new Date(startDate);
  const end = new Date(endDate);
  start.setHours(0, 0, 0, 0);
  end.setHours(0, 0, 0, 0);
  if (end < start) return 0;

  let workingDays = 0;
  const cur = new Date(start);
  while (cur <= end) {
    const dayOfWeek = cur.getDay();
    if (dayOfWeek !== 0 && dayOfWeek !== 6) { // 0 = Sunday, 6 = Saturday
      workingDays++;
    }
    cur.setDate(cur.getDate() + 1);
  }
  return Math.max(1, workingDays);
}
