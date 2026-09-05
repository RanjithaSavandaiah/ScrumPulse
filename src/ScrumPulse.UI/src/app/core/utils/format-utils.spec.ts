import {
  cleanName,
  getInitials,
  getRoleLabel,
  getBadgeLabel,
  isDeliveryRole,
  isLeadershipRole
} from './format-utils';

describe('format-utils', () => {
  describe('cleanName', () => {
    it('should strip role in parentheses', () => {
      expect(cleanName('Priya Sharma (Developer)')).toBe('Priya Sharma');
      expect(cleanName('Sam Smith (QA Engineer)')).toBe('Sam Smith');
      expect(cleanName('No Parentheses')).toBe('No Parentheses');
      expect(cleanName('')).toBe('');
      expect(cleanName(null)).toBe('');
      expect(cleanName(undefined)).toBe('');
    });
  });

  describe('getInitials', () => {
    it('should extract two uppercase letters from first and last parts', () => {
      expect(getInitials('Priya Sharma')).toBe('PS');
      expect(getInitials('John Middle Doe')).toBe('JD');
      expect(getInitials('Kaushik')).toBe('KA');
      expect(getInitials('')).toBe('??');
      expect(getInitials(null)).toBe('??');
    });
  });

  describe('getRoleLabel', () => {
    it('should map numeric and string role representations to human labels', () => {
      expect(getRoleLabel('0')).toBe('Scrum Master');
      expect(getRoleLabel('ScrumMaster')).toBe('Scrum Master');
      expect(getRoleLabel('1')).toBe('Developer');
      expect(getRoleLabel('Developer')).toBe('Developer');
      expect(getRoleLabel('2')).toBe('QA Engineer');
      expect(getRoleLabel('QaEngineer')).toBe('QA Engineer');
      expect(getRoleLabel('3')).toBe('CDL');
      expect(getRoleLabel('Cdl')).toBe('CDL');
      expect(getRoleLabel('4')).toBe('Product Owner');
      expect(getRoleLabel('ProductOwner')).toBe('Product Owner');
      expect(getRoleLabel('ClientStakeholder')).toBe('Product Owner');
      expect(getRoleLabel('5')).toBe('Agile Coach');
      expect(getRoleLabel('AgileCoach')).toBe('Agile Coach');
      expect(getRoleLabel(null)).toBe('');
      expect(getRoleLabel('CustomRole')).toBe('CustomRole');
    });
  });

  describe('getBadgeLabel', () => {
    it('should map badge keys and numbers to labels', () => {
      expect(getBadgeLabel(null)).toBe('Kudos');
      expect(getBadgeLabel('ProblemSolver')).toBe('Problem Solver');
      expect(getBadgeLabel('TeamPlayer')).toBe('Team Player');
      expect(getBadgeLabel('Innovator')).toBe('Innovator');
      expect(getBadgeLabel('LifeSaver')).toBe('Life Saver');
      expect(getBadgeLabel('Speedy')).toBe('Speed Demon');
    });
  });

  describe('isDeliveryRole and isLeadershipRole', () => {
    it('should classify delivery roles correctly', () => {
      expect(isDeliveryRole('Developer')).toBeTrue();
      expect(isDeliveryRole('QaEngineer')).toBeTrue();
      expect(isDeliveryRole('ScrumMaster')).toBeFalse();
      expect(isDeliveryRole(null)).toBeFalse();
    });

    it('should classify leadership roles correctly', () => {
      expect(isLeadershipRole('ScrumMaster')).toBeTrue();
      expect(isLeadershipRole('Cdl')).toBeTrue();
      expect(isLeadershipRole('AgileCoach')).toBeTrue();
      expect(isLeadershipRole('ProductOwner')).toBeTrue();
      expect(isLeadershipRole('ClientStakeholder')).toBeTrue();
      expect(isLeadershipRole('Developer')).toBeFalse();
      expect(isLeadershipRole(null)).toBeFalse();
    });
  });
});
