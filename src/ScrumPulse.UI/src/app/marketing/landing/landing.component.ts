import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PageMetadataService } from '../page-metadata.service';

/**
 * Public home page.
 *
 * This replaced the dashboard at the site root. Previously a first-time visitor, and any
 * crawler, landed on a squad dashboard that showed nothing without a join code — which is
 * both a poor introduction and the reason the site read as having no content. The
 * application now lives at /app and this page explains what it is.
 */
@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [RouterLink],
  styleUrl: '../marketing.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="marketing-header">
      <a routerLink="/" class="brand">Scrum<span>Pulse</span></a>
      <nav>
        <a routerLink="/blog">Notes</a>
        <a routerLink="/about">About</a>
        <a routerLink="/contact">Contact</a>
        <a routerLink="/app" class="btn-launch">Launch Dashboard &rarr;</a>
      </nav>
    </header>

    <main class="marketing-page wide">
      <section class="hero">
        <h1>See where your sprint is actually losing time</h1>
        <p class="tagline">
          ScrumPulse tracks the things a burndown chart hides: how long code waits for review,
          how long blockers sit unresolved, and how much capacity leave has quietly removed.
          Built by a scrum master for teams that want evidence rather than a feeling.
        </p>

        <div class="hero-actions">
          <a class="cta cta-primary" routerLink="/app">Launch Dashboard &amp; Tabs &rarr;</a>
          <a class="cta cta-secondary" routerLink="/blog">Read delivery notes</a>
          <a class="cta cta-secondary" routerLink="/about">About the author</a>
        </div>
      </section>

      <h2>What it measures</h2>
      <p>
        Most delivery tooling tells you what is in progress. ScrumPulse is built around the
        question of where time goes between starting a piece of work and shipping it, because
        that gap is where predictability is won or lost.
      </p>

      <div class="feature-grid">
        <article class="feature-card">
          <h3>Cycle time by stage</h3>
          <p>
            Seven timestamps from backlog to done, so you can see whether work is slow to build
            or slow to be reviewed. They have different fixes.
          </p>
        </article>

        <article class="feature-card">
          <h3>Blocker SLA radar</h3>
          <p>
            Every blocker carries a working-hours clock and a root cause. Escalation stops being
            a personal decision and becomes a threshold.
          </p>
        </article>

        <article class="feature-card">
          <h3>Capacity-adjusted velocity</h3>
          <p>
            Net focus hours after approved leave and working days, so a sprint with two people
            away is not compared against one at full strength.
          </p>
        </article>

        <article class="feature-card">
          <h3>Say-do predictability</h3>
          <p>
            Committed against delivered across sprints, presented as a forecast you can plan
            around rather than a score to defend.
          </p>
        </article>

        <article class="feature-card">
          <h3>Review turnaround</h3>
          <p>
            Pull request latency and the ratio of actionable to total comments, measured per
            item rather than per person.
          </p>
        </article>

        <article class="feature-card">
          <h3>AI delivery coach</h3>
          <p>
            Grounded analysis of your own metrics, and a standup scribe that drafts updates from
            what was said on the call.
          </p>
        </article>
      </div>

      <h2>Built around one opinion</h2>
      <p>
        A metric that cannot change a decision is decoration. Every number here is attached to
        something a scrum master can act on this sprint: a review window to introduce, a blocker
        to escalate, a commitment to adjust. Metrics that only produce a chart were deliberately
        left out.
      </p>
      <p>
        That extends to how the measurements are framed. Per-stage timings sit on the work item
        rather than the person, because the moment review latency becomes an individual's score,
        reviews get fast and stop being reviews.
      </p>

      <h2>Where to start</h2>
      <p>
        The <a routerLink="/blog">delivery notes</a> cover the reasoning behind each metric in
        more depth — why the blocker SLA is eight working hours, and why say-do is a forecast
        rather than a score.
      </p>

      <div class="hero-actions" style="justify-content: flex-start; margin-top: 2rem">
        <a class="cta cta-primary" routerLink="/app">Launch Dashboard &amp; Tabs &rarr;</a>
        <a class="cta cta-secondary" routerLink="/blog">Read delivery notes</a>
        <a class="cta cta-secondary" routerLink="/contact">Get in touch</a>
      </div>
    </main>
  `
})
export class LandingComponent {
  constructor() {
    inject(PageMetadataService).apply({
      title: 'ScrumPulse — sprint delivery metrics for scrum masters',
      description:
        'Track cycle time by stage, blocker SLAs, capacity-adjusted velocity and review turnaround. A delivery metrics tool built by a scrum master.',
      path: '/'
    });
  }
}
