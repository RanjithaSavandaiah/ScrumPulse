import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

/**
 * Navigation shared by every public page.
 *
 * Previously only the landing page carried a header, so anyone who followed a link into an
 * article, About or Contact had no route back and no way to reach the application except the
 * browser's back button. Extracting it into one component fixes that everywhere at once and
 * keeps a single definition of the public navigation, rather than the same markup pasted into
 * five templates and drifting.
 *
 * "Launch Dashboard" is the deliberate exit into the application, which lives at /app.
 */
@Component({
  selector: 'app-marketing-header',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  styleUrl: './marketing.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="marketing-header">
      <a routerLink="/" class="brand" aria-label="ScrumPulse home">Scrum<span>Pulse</span></a>

      <nav aria-label="Primary">
        <!--
          routerLinkActive marks the current section so a reader landing from search knows
          where they are. The home link is excluded: with exact matching off it would stay
          active on every page.
        -->
        <a routerLink="/blog" routerLinkActive="is-active">Notes</a>
        <a routerLink="/about" routerLinkActive="is-active">About</a>
        <a routerLink="/contact" routerLinkActive="is-active">Contact</a>
        <a routerLink="/app" class="btn-launch">Launch Dashboard &rarr;</a>
      </nav>
    </header>
  `
})
export class MarketingHeaderComponent {}
