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
  return {
    startDate: start.toISOString().split('T')[0],
    endDate: end.toISOString().split('T')[0]
  };
}

/**
 * Calculates date range for "This Month" (from 1st of month to today).
 */
export function getThisMonthDateRange(): DateRangePreset {
  const now = new Date();
  const firstDay = new Date(now.getFullYear(), now.getMonth(), 1);
  return {
    startDate: firstDay.toISOString().split('T')[0],
    endDate: now.toISOString().split('T')[0]
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
