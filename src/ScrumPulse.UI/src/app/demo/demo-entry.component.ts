import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { DemoModeService } from './demo-mode.service';

/**
 * Turns demo mode on, then hands over to the application.
 *
 * A route rather than a query parameter on /app, so the entry point is a clean shareable URL
 * and the application itself needs no knowledge of how demo mode was requested. The redirect
 * replaces this entry in history, so the browser back button returns to the page the visitor
 * arrived from rather than re-triggering the demo.
 */
@Component({
  selector: 'app-demo-entry',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="demo-loading" role="status">Loading the ScrumPulse demo…</p>
  `,
  styles: [
    `
      .demo-loading {
        padding: 4rem 1.5rem;
        text-align: center;
        color: var(--text-secondary);
      }
    `
  ]
})
export class DemoEntryComponent {
  constructor() {
    inject(DemoModeService).enable();
    void inject(Router).navigate(['/app'], { replaceUrl: true });
  }
}
