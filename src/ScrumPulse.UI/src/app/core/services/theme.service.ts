import { PLATFORM_ID, Injectable, computed, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

export type ThemeName = 'dark' | 'light';

/** Owns the active colour theme: applies it to the document, shares it, remembers it. */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private static readonly StorageKey = 'scrumpulse.theme';

  /** False while prerendering in Node, where there is no DOM, localStorage or matchMedia. */
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private readonly activeTheme = signal<ThemeName>(this.resolveInitialTheme());

  readonly theme = this.activeTheme.asReadonly();
  readonly isDark = computed(() => this.activeTheme() === 'dark');

  constructor() {
    // The inline boot script may have set this already, but a stored preference resolved
    // here can differ from what it guessed.
    this.applyToDocument(this.activeTheme());
  }

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
      // Private browsing and locked-down enterprise policies make writes throw. The theme
      // still applies for this session; only remembering it is lost.
    }
  }

  /** Stored choice first, then the OS preference, then the product default. */
  private resolveInitialTheme(): ThemeName {
    // Prerendered HTML uses the default; the browser corrects it on hydration.
    if (!this.isBrowser) {
      return 'dark';
    }

    try {
      const stored = localStorage.getItem(ThemeService.StorageKey);

      if (stored === 'dark' || stored === 'light') {
        return stored;
      }
    } catch {
      // Unreadable storage falls through to the OS setting.
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
