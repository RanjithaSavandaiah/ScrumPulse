import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

/**
 * Tracks whether the application is running as the public demo.
 *
 * Demo mode is entered by visiting /demo and persists for the tab so that navigating between
 * tabs inside the app does not drop out of it. It is deliberately session-scoped rather than
 * stored in localStorage: a visitor who looked at the demo once should not find the real
 * application still serving fixtures a week later.
 */
@Injectable({ providedIn: 'root' })
export class DemoModeService {
  private static readonly StorageKey = 'scrumpulse.demoMode';

  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly active = signal(this.readStoredFlag());

  /** True while the application should serve fixture data instead of calling the API. */
  readonly isActive = this.active.asReadonly();

  enable(): void {
    this.active.set(true);
    this.writeStoredFlag(true);
  }

  disable(): void {
    this.active.set(false);
    this.writeStoredFlag(false);
  }

  private readStoredFlag(): boolean {
    if (!this.isBrowser) {
      return false;
    }

    try {
      return sessionStorage.getItem(DemoModeService.StorageKey) === 'true';
    } catch {
      return false;
    }
  }

  private writeStoredFlag(isActive: boolean): void {
    if (!this.isBrowser) {
      return;
    }

    try {
      if (isActive) {
        sessionStorage.setItem(DemoModeService.StorageKey, 'true');
      } else {
        sessionStorage.removeItem(DemoModeService.StorageKey);
      }
    } catch {
      // Storage unavailable. Demo mode still works for this page load; it simply will not
      // survive navigation, which is a far smaller problem than failing outright.
    }
  }
}
