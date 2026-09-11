import {
  generateDynamicMonths,
  generateCalendarYearMonths,
  getCalendarYearRange,
  toDateOnlyString,
  isLeaveInPeriod,
  generateDynamicQuarters,
  getCurrentMonthValue,
  getCurrentQuarterValue,
  getDatePresetRange,
  getThisMonthDateRange,
  getSprintDateRange,
  calculateWorkingHours
} from './date-utils';

describe('date-utils', () => {
  describe('generateDynamicMonths and generateCalendarYearMonths', () => {
    it('should generate rolling list of months with current indicator', () => {
      const months = generateDynamicMonths(2, 1, true);
      expect(months.length).toBe(5); // ALL + 4 months
      expect(months[0].value).toBe('ALL');
      expect(months.some(month => month.label.includes('(Current)'))).toBeTrue();
    });

    it('should generate all 12 calendar year months', () => {
      const months = generateCalendarYearMonths(2026);
      expect(months.length).toBe(12);
      expect(months[0].value).toBe('2026-01');
      expect(months[11].value).toBe('2026-12');
    });
  });

  describe('toDateOnlyString', () => {
    it('should format date safely to YYYY-MM-DD', () => {
      expect(toDateOnlyString('2026-09-05T12:00:00Z')).toBe('2026-09-05');
      expect(toDateOnlyString(new Date(2026, 8, 5))).toBe('2026-09-05');
      expect(toDateOnlyString(null)).toBeNull();
      expect(toDateOnlyString('invalid-date')).toBeNull();
    });
  });

  describe('isLeaveInPeriod', () => {
    const leave = {
      startDate: '2026-09-10',
      endDate: '2026-09-15'
    };

    it('should return true for ALL period', () => {
      expect(isLeaveInPeriod(leave, 'ALL')).toBeTrue();
    });

    it('should match YEAR period strictly within bounds', () => {
      expect(isLeaveInPeriod(leave, 'YEAR_2026')).toBeTrue();
      expect(isLeaveInPeriod(leave, 'YEAR_2025')).toBeFalse();
    });

    it('should match specific month', () => {
      expect(isLeaveInPeriod(leave, '2026-09')).toBeTrue();
      expect(isLeaveInPeriod(leave, '2026-08')).toBeFalse();
    });

    it('should match custom date range', () => {
      expect(isLeaveInPeriod(leave, 'CUSTOM', '2026-09-01', '2026-09-30')).toBeTrue();
      expect(isLeaveInPeriod(leave, 'CUSTOM', '2026-09-12', '2026-09-20')).toBeTrue();
      expect(isLeaveInPeriod(leave, 'CUSTOM', '2026-10-01', '2026-10-10')).toBeFalse();
    });
  });

  describe('Quarters and Presets', () => {
    it('should generate dynamic quarters', () => {
      const quarters = generateDynamicQuarters(3, 1);
      expect(quarters.length).toBe(5);
      expect(quarters.some(quarter => quarter.value.includes('-Q'))).toBeTrue();
    });

    it('should return valid current month and quarter values', () => {
      expect(getCurrentMonthValue()).toMatch(/^\d{4}-\d{2}$/);
      expect(getCurrentQuarterValue()).toMatch(/^\d{4}-Q[1-4]$/);
    });

    it('should compute date preset ranges correctly', () => {
      const range7 = getDatePresetRange(7);
      expect(range7.startDate).toBeDefined();
      expect(range7.endDate).toBeDefined();

      const thisMonth = getThisMonthDateRange();
      expect(thisMonth.startDate).toMatch(/^\d{4}-\d{2}-01$/);

      const sprintRange = getSprintDateRange('2026-09-01T00:00:00Z', '2026-09-14T00:00:00Z');
      expect(sprintRange?.startDate).toBe('2026-09-01');
      expect(sprintRange?.endDate).toBe('2026-09-14');

      expect(getSprintDateRange(undefined, undefined)).toBeNull();
    });
  });

  describe('calculateWorkingHours', () => {
    it('should calculate same day business hours correctly within 9:00 to 17:30', () => {
      // Monday 10:00 to 14:30 = 4.5h
      const start = '2026-08-03T10:00:00';
      const end = '2026-08-03T14:30:00';
      expect(calculateWorkingHours(start, end)).toBe(4.5);
    });

    it('should exclude overnight non-working hours between days', () => {
      // Wednesday 16:00 (4:00 PM) to Thursday 11:00 AM
      // Wed: 16:00 to 17:30 = 1.5h
      // Overnight: 17:30 to 09:00 excluded (0h)
      // Thu: 09:00 to 11:00 = 2.0h
      // Total: 3.5h (calendar hours would be 19h!)
      const start = '2026-08-05T16:00:00';
      const end = '2026-08-06T11:00:00';
      expect(calculateWorkingHours(start, end)).toBe(3.5);
    });

    it('should exclude weekends across multiple days', () => {
      // Friday 16:00 to Monday 11:00 AM
      // Fri: 1.5h + Sat/Sun: 0h + Mon: 2.0h = 3.5h
      const start = '2026-08-07T16:00:00';
      const end = '2026-08-10T11:00:00';
      expect(calculateWorkingHours(start, end)).toBe(3.5);
    });

    it('should return 0 for weekend-only intervals or invalid dates', () => {
      const saturday = '2026-08-08T10:00:00';
      const sunday = '2026-08-09T14:00:00';
      expect(calculateWorkingHours(saturday, sunday)).toBe(0);
      expect(calculateWorkingHours(null, sunday)).toBe(0);
      expect(calculateWorkingHours(sunday, saturday)).toBe(0);
    });
  });
});

