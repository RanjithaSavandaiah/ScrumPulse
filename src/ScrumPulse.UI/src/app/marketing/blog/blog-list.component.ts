import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PageMetadataService } from '../page-metadata.service';
import { postsNewestFirst } from './blog-posts.data';

/** Index of published articles. */
@Component({
  selector: 'app-blog-list',
  standalone: true,
  imports: [RouterLink, DatePipe],
  styleUrl: '../marketing.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="marketing-page">
      <h1>Delivery notes</h1>
      <p>
        Working notes on sprint metrics, flow and scrum practice — the reasoning behind how
        ScrumPulse measures things, and what changed on the teams it came from.
      </p>

      <div class="post-list">
        @for (post of posts; track post.slug) {
          <a class="post-card" [routerLink]="['/blog', post.slug]">
            <h2>{{ post.title }}</h2>
            <p>{{ post.summary }}</p>

            <div class="post-meta">
              <span class="topic-tag">{{ post.topic }}</span>
              <span>{{ post.publishedOn | date: 'd MMMM yyyy' }}</span>
              <span>{{ post.readingMinutes }} min read</span>
            </div>
          </a>
        }
      </div>
    </main>
  `
})
export class BlogListComponent {
  protected readonly posts = postsNewestFirst();

  constructor() {
    inject(PageMetadataService).apply({
      title: 'Delivery notes — ScrumPulse',
      description:
        'Notes on sprint metrics, cycle time, blocker SLAs and scrum practice from a working scrum master.',
      path: '/blog'
    });
  }
}
