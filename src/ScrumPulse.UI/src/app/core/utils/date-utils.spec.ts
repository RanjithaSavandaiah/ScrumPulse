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
  getSprintDateRange
} from './date-utils';

describe('date-utils', () => {
  describe('generateDynamicMonths and generateCalendarYearMonths', () => {
    it('should generate rolling list of months with current indicator', () => {
      const months = generateDynamicMonths(2, 1, true);
      expect(months.length).toBe(5); // ALL + 4 months
      expect(months[0].value).toBe('ALL');
      expect(months.some(m => m.label.includes('(Current)'))).toBeTrue();
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
      expect(quarters.some(q => q.value.includes('-Q'))).toBeTrue();
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
});
