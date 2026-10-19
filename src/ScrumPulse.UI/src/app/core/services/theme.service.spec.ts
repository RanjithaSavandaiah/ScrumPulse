import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme.service';

/**
 * Covers theme selection and persistence.
 *
 * The bug being guarded against: the navbar previously held `isDark` as a plain field, so the
 * toggle icon reset on every reload regardless of the theme actually applied to the document,
 * and the choice itself was never stored.
 */
describe('ThemeService', () => {
  const storageKey = 'scrumpulse.theme';

  function createService(): ThemeService {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    return TestBed.inject(ThemeService);
  }

  function appliedTheme(): string | null {
    return document.documentElement.getAttribute('data-theme');
  }

  beforeEach(() => localStorage.removeItem(storageKey));

  afterEach(() => {
    localStorage.removeItem(storageKey);
    document.documentElement.removeAttribute('data-theme');
  });

  it('applies the resolved theme to the document on creation', () => {
    createService();

    expect(appliedTheme()).toMatch(/^(dark|light)$/);
  });

  it('toggles between dark and light', () => {
    const service = createService();
    const startedDark = service.isDark();

    service.toggle();

    expect(service.isDark()).toBe(!startedDark);
    expect(appliedTheme()).toBe(startedDark ? 'light' : 'dark');
  });

  it('persists the choice', () => {
    const service = createService();
    service.setTheme('light');

    expect(localStorage.getItem(storageKey)).toBe('light');
  });

  it('restores the stored choice on the next load', () => {
    // The regression this exists to catch: the theme was written but never read back.
    localStorage.setItem(storageKey, 'light');

    const service = createService();

    expect(service.theme()).toBe('light');
    expect(service.isDark()).toBeFalse();
    expect(appliedTheme()).toBe('light');
  });

  it('prefers an explicit past choice over the operating system setting', () => {
    localStorage.setItem(storageKey, 'dark');

    expect(createService().theme()).toBe('dark');
  });

  it('ignores a stored value that is not a known theme', () => {
    // Local storage is user-editable, so an unexpected value must not reach the attribute.
    localStorage.setItem(storageKey, 'neon');

    expect(createService().theme()).toMatch(/^(dark|light)$/);
  });

  it('keeps working when local storage cannot be written', () => {
    // Private browsing and hardened enterprise policies make this throw.
    const service = createService();
    spyOn(Storage.prototype, 'setItem').and.throwError('denied');

    expect(() => service.setTheme('light')).not.toThrow();
    expect(service.theme()).toBe('light');
  });

  it('keeps working when local storage cannot be read', () => {
    spyOn(Storage.prototype, 'getItem').and.throwError('denied');

    expect(() => createService()).not.toThrow();
  });
});
