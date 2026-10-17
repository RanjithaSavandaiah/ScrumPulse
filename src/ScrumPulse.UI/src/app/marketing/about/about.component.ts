import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PageMetadataService } from '../page-metadata.service';

/**
 * About page.
 *
 * Establishes who built the product and from what experience. Beyond being a page visitors
 * look for, first-hand authorship is the signal that distinguishes a tool written by someone
 * doing the job from assembled content, which is what reviewers and search ranking both
 * weigh.
 */
@Component({
  selector: 'app-about',
  standalone: true,
  imports: [RouterLink],
  styleUrl: '../marketing.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="marketing-page">
      <h1>About ScrumPulse</h1>

      <p>
        ScrumPulse is built and maintained by
        <strong>Ranjitha Savandaiah</strong>, a scrum master working with distributed delivery
        teams. It started as a way to answer a question that kept coming up in sprint reviews
        and that no tool on hand could answer: not <em>what</em> was delivered, but
        <em>where the time went</em>.
      </p>

      <h2>Why it exists</h2>
      <p>
        Standard delivery tooling is good at recording state. It will tell you what is in
        progress, what is done, and how many points closed. It is much weaker at the questions
        a scrum master actually gets asked — why the sprint slipped, whether the next one is
        realistic, and what to change on Monday.
      </p>
      <p>
        Answering those needs elapsed time rather than status: how long work waits between
        stages, how long blockers sit unresolved, and how much capacity leave has removed
        before anyone commits to anything. Those numbers existed in scattered places and nowhere
        together, so the reporting was manual, inconsistent, and produced too late to change the
        sprint it described.
      </p>

      <h2>The principle it is built on</h2>
      <p>
        A metric that cannot change a decision is decoration. Every measurement in ScrumPulse
        attaches to an action available this sprint — a review window to introduce, a blocker to
        escalate, a commitment to revise. Measurements that only produce a chart were left out
        deliberately, however good they look in a report.
      </p>
      <p>
        A second principle shapes how they are framed: the measurements sit on the work, not on
        people. Per-stage timings make it very easy to derive individual scorecards, and the
        moment review latency becomes somebody's number, pull requests get approved quickly and
        read by nobody. The metric would improve and the practice would get worse.
      </p>

      <h2>How it is built</h2>
      <p>
        ASP.NET Core on .NET 10 with a clean-architecture separation, Angular on the front end,
        PostgreSQL in production. The AI coaching runs on the Microsoft Agent Framework over
        Groq and Gemini, and degrades to a deterministic rule-based analysis when the model
        quota is exhausted rather than failing — the reasoning being that a tool which stops
        working when a free tier runs out is not a tool anyone can rely on.
      </p>
      <p>
        The <a routerLink="/blog">delivery notes</a> go into the reasoning behind particular
        metrics in more depth.
      </p>

      <h2>Contact</h2>
      <p>
        Feedback, corrections and disagreement are all welcome — particularly disagreement about
        the metrics, which is how they get better. The <a routerLink="/contact">contact page</a>
        has the ways to get in touch.
      </p>
    </main>
  `
})
export class AboutComponent {
  constructor() {
    inject(PageMetadataService).apply({
      title: 'About ScrumPulse and its author',
      description:
        'ScrumPulse is built by Ranjitha Savandaiah, a scrum master working with distributed delivery teams. Why the tool exists and the principles behind its metrics.',
      path: '/about'
    });
  }
}
