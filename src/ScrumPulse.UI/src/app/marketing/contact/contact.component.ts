import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PageMetadataService } from '../page-metadata.service';

/**
 * Contact page.
 *
 * Deliberately lists real destinations rather than presenting a form. A form needs a backend
 * mail path and spam handling to be anything other than a button that silently discards
 * messages, and a contact page that loses messages is worse than none.
 */
@Component({
  selector: 'app-contact',
  standalone: true,
  imports: [RouterLink],
  styleUrl: '../marketing.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="marketing-page">
      <h1>Contact</h1>

      <p>
        Questions about ScrumPulse, feedback on the metrics, or a bug to report — any of these
        are welcome. Messages go directly to the person who builds it.
      </p>

      <div class="contact-methods">
        <article class="contact-card">
          <h3>Email</h3>
          <p>
            <a href="mailto:{{ contactEmail }}">{{ contactEmail }}</a>
          </p>
        </article>

        <article class="contact-card">
          <h3>GitHub</h3>
          <p>
            Issues and pull requests at
            <a [href]="githubUrl" target="_blank" rel="noopener noreferrer">
              github.com/RanjithaSavandaiah
            </a>
          </p>
        </article>

        <article class="contact-card">
          <h3>LinkedIn</h3>
          <p>
            <a [href]="linkedInUrl" target="_blank" rel="noopener noreferrer">
              Ranjitha Savandaiah
            </a>
          </p>
        </article>
      </div>

      <h2>Reporting a problem</h2>
      <p>
        If something is broken, the most useful report says what you expected, what happened,
        and roughly when. If it involves a specific sprint or work item, the squad name helps —
        please do not paste anything confidential to your organisation.
      </p>

      <h2>Security issues</h2>
      <p>
        If you have found a vulnerability, please report it privately by email rather than
        opening a public issue, so it can be fixed before it is described publicly. Reports are
        acknowledged and taken seriously.
      </p>

      <h2>Your data</h2>
      <p>
        What the application stores and why is set out in the
        <a routerLink="/privacy-policy">privacy policy</a>. If you want data removed, email is
        the fastest route.
      </p>
    </main>
  `
})
export class ContactComponent {
  protected readonly contactEmail = 'ranjithasavandaiah@gmail.com';
  protected readonly githubUrl = 'https://github.com/RanjithaSavandaiah';
  protected readonly linkedInUrl = 'https://www.linkedin.com/in/ranjitha-savandaiah';

  constructor() {
    inject(PageMetadataService).apply({
      title: 'Contact — ScrumPulse',
      description:
        'Get in touch about ScrumPulse: feedback on the delivery metrics, bug reports, security disclosures, or data removal requests.',
      path: '/contact'
    });
  }
}
