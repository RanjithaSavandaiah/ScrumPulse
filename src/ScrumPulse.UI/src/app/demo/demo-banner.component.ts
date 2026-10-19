import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map } from 'rxjs';
import { DemoModeService } from './demo-mode.service';

/**
 * Persistent notice shown while the demo is running.
 *
 * Two jobs. It stops anyone mistaking the fictional squad for real data - important when the
 * numbers look plausible and the names do not obviously read as invented. And it explains in
 * advance why saving is refused, so a visitor who tries to add a work item understands the
 * result rather than concluding the product is broken.
 *
 * Renders nothing outside demo mode or outside application workspace screens.
 */
@Component({
  selector: 'app-demo-banner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (demoMode.isActive() && isAppRoute()) {
      <div class="demo-banner" role="status">
        <span class="demo-badge">Demo</span>

        <p class="demo-text">
          Sample data for a fictional squad. Nothing you change here is saved.
        </p>

        <button type="button" class="demo-exit" (click)="exitDemo()">Exit demo</button>
      </div>
    }
  `,
  styles: [
    `
      .demo-banner {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        padding: 0.6rem 1.25rem;
        background: color-mix(in srgb, var(--accent-warning) 16%, transparent);
        border-bottom: 1px solid color-mix(in srgb, var(--accent-warning) 40%, transparent);
      }

      .demo-badge {
        flex-shrink: 0;
        padding: 0.15rem 0.6rem;
        font-size: 0.72rem;
        font-weight: 700;
        letter-spacing: 0.04em;
        text-transform: uppercase;
        color: var(--accent-warning);
        background: color-mix(in srgb, var(--accent-warning) 20%, transparent);
        border-radius: var(--radius-full);
      }

      .demo-text {
        flex: 1;
        margin: 0;
        font-size: 0.86rem;
        color: var(--text-primary);
      }

      .demo-exit {
        flex-shrink: 0;
        padding: 0.3rem 0.8rem;
        font-size: 0.82rem;
        font-weight: 600;
        color: var(--text-primary);
        cursor: pointer;
        background: transparent;
        border: 1px solid var(--border-glass);
        border-radius: var(--radius-sm);
      }

      .demo-exit:hover {
        background: var(--bg-glass);
      }

      /* The explanatory line is the first thing to drop on a narrow screen; the badge alone
         still communicates that this is not live data. */
      @media (max-width: 640px) {
        .demo-banner {
          padding: 0.5rem 0.85rem;
          gap: 0.5rem;
        }

        .demo-text {
          font-size: 0.78rem;
        }
      }
    `
  ]
})
export class DemoBannerComponent {
  protected readonly demoMode = inject(DemoModeService);
  private readonly router = inject(Router);

  private readonly currentUrl = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map((e) => e.urlAfterRedirects || e.url)
    ),
    { initialValue: this.router.url }
  );

  protected readonly isAppRoute = computed(() => {
    const url = this.currentUrl() ?? '';
    const cleanPath = url.split('?')[0].split('#')[0];
    return cleanPath.startsWith('/app') || cleanPath === '/dashboard';
  });

  /** Leaves demo mode and returns to the public home page. */
  protected exitDemo(): void {
    this.demoMode.disable();
    void this.router.navigate(['/']);
  }
}
