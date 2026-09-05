import { BadgeLabelPipe } from './badge-label.pipe';
import { CleanNamePipe } from './clean-name.pipe';
import { InitialsPipe } from './initials.pipe';
import { RoleLabelPipe } from './role-label.pipe';

describe('Core Pipes', () => {
  describe('BadgeLabelPipe', () => {
    let pipe: BadgeLabelPipe;

    beforeEach(() => {
      pipe = new BadgeLabelPipe();
    });

    it('should transform badge keys and null safely', () => {
      expect(pipe.transform(null)).toBe('Kudos');
      expect(pipe.transform(undefined)).toBe('Kudos');
      expect(pipe.transform('ProblemSolver')).toBe('Problem Solver');
      expect(pipe.transform('TeamPlayer')).toBe('Team Player');
      expect(pipe.transform('Innovator')).toBe('Innovator');
    });
  });

  describe('CleanNamePipe', () => {
    let pipe: CleanNamePipe;

    beforeEach(() => {
      pipe = new CleanNamePipe();
    });

    it('should remove parenthetical role tags', () => {
      expect(pipe.transform('Priya Sharma (Developer)')).toBe('Priya Sharma');
      expect(pipe.transform('Alex Morgan (QA)')).toBe('Alex Morgan');
      expect(pipe.transform('Kaushik')).toBe('Kaushik');
      expect(pipe.transform(null)).toBe('');
      expect(pipe.transform(undefined)).toBe('');
    });
  });

  describe('InitialsPipe', () => {
    let pipe: InitialsPipe;

    beforeEach(() => {
      pipe = new InitialsPipe();
    });

    it('should generate 2-letter uppercase initials', () => {
      expect(pipe.transform('Priya Sharma')).toBe('PS');
      expect(pipe.transform('Alex Morgan (QA)')).toBe('AM');
      expect(pipe.transform('Kaushik')).toBe('KA');
      expect(pipe.transform(null)).toBe('??');
      expect(pipe.transform('')).toBe('??');
    });
  });

  describe('RoleLabelPipe', () => {
    let pipe: RoleLabelPipe;

    beforeEach(() => {
      pipe = new RoleLabelPipe();
    });

    it('should map role keys to readable labels', () => {
      expect(pipe.transform('ScrumMaster')).toBe('Scrum Master');
      expect(pipe.transform('Developer')).toBe('Developer');
      expect(pipe.transform('QaEngineer')).toBe('QA Engineer');
      expect(pipe.transform('Cdl')).toBe('CDL');
      expect(pipe.transform('ProductOwner')).toBe('Product Owner');
      expect(pipe.transform('AgileCoach')).toBe('Agile Coach');
      expect(pipe.transform(null)).toBe('');
    });
  });
});
