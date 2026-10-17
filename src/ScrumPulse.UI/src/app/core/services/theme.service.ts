import { PLATFORM_ID, Injectable, computed, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

/** The two themes the stylesheet defines. */
export type ThemeName = 'dark' | 'light';

/**
 * Owns the active colour theme and keeps it applied, shared, and remembered.
 *
 * Previously the navbar held `isDark` as a plain field and wrote `data-theme` to the document
 * itself. That had three consequences: the choice was lost on reload because nothing persisted
 * it, any other component wanting to know the theme had no way to ask, and the field drifted
 * out of step with the attribute actually on the document.
 *
 * Holding it here as a signal makes the theme one shared piece of state that survives a reload
 * and that any component can read.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  /** Storage key. A plain UI preference, so no session data is involved. */
  private static readonly StorageKey = 'scrumpulse.theme';

  /**
   * False while prerendering, where there is no DOM, no localStorage and no matchMedia.
   *
   * The public pages are prerendered in Node, so every browser API this service touches has
   * to be guarded. Without this the build crashes rather than producing the static HTML that
   * makes the content visible to crawlers.
   */
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private readonly activeTheme = signal<ThemeName>(this.resolveInitialTheme());

  readonly theme = this.activeTheme.asReadonly();
  readonly isDark = computed(() => this.activeTheme() === 'dark');

  constructor() {
    // The document may already carry a theme from the inline boot script, but applying it
    // here too keeps the attribute correct once a stored preference has been resolved.
    this.applyToDocument(this.activeTheme());
  }

  /** Switches between dark and light, persisting the choice. */
  toggle(): void {
    this.setTheme(this.isDark() ? 'light' : 'dark');
  }

  setTheme(theme: ThemeName): void {
    this.activeTheme.set(theme);
    this.applyToDocument(theme);

    if (!this.isBrowser) {
      return;
    }

    try {
      localStorage.setItem(ThemeService.StorageKey, theme);
    } catch {
      // Private browsing and hardened enterprise policies can make localStorage throw on
      // write. The theme still applies for this session; only remembering it is lost, which
      // is not worth surfacing to the user.
    }
  }

  /**
   * Chooses the theme for a fresh page load.
   *
   * Order is deliberate: an explicit past choice wins, then the operating system preference,
   * then the product default. Respecting the OS setting matters for anyone who runs light
   * mode for readability rather than taste.
   */
  private resolveInitialTheme(): ThemeName {
    // Prerendering has no stored preference and no OS setting to read, so the static HTML is
    // generated in the product default. The browser corrects it on hydration.
    if (!this.isBrowser) {
      return 'dark';
    }

    try {
      const stored = localStorage.getItem(ThemeService.StorageKey);

      if (stored === 'dark' || stored === 'light') {
        return stored;
      }
    } catch {
      // Unreadable storage is not an error worth reporting; fall through to the OS setting.
    }

    const prefersLight =
      typeof window !== 'undefined' && window.matchMedia?.('(prefers-color-scheme: light)').matches;

    return prefersLight ? 'light' : 'dark';
  }

  private applyToDocument(theme: ThemeName): void {
    if (!this.isBrowser) {
      return;
    }

    document.documentElement.setAttribute('data-theme', theme);
  }
}
