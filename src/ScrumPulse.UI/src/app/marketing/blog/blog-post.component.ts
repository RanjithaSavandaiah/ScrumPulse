import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { MarketingHeaderComponent } from '../marketing-header.component';
import { PageMetadataService } from '../page-metadata.service';
import { findPostBySlug } from './blog-posts.data';

/**
 * Renders one article.
 *
 * The slug arrives as a routed input rather than by subscribing to ParamMap, so there is no
 * subscription to clean up and the component re-renders on navigation between two articles.
 */
@Component({
  selector: 'app-blog-post',
  standalone: true,
  imports: [RouterLink, DatePipe, MarketingHeaderComponent],
  styleUrl: '../marketing.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (post(); as article) {
      <app-marketing-header />

      <main class="marketing-page">
        <a class="back-link" routerLink="/blog">← All delivery notes</a>

        <article>
          <header class="article-header">
            <h1>{{ article.title }}</h1>
            <div class="post-meta">
              <span class="topic-tag">{{ article.topic }}</span>
              <span>{{ article.publishedOn | date: 'd MMMM yyyy' }}</span>
              <span>{{ article.readingMinutes }} min read</span>
              <span>Ranjitha Savandaiah</span>
            </div>
          </header>

          <!--
            Article bodies are authored as HTML in blog-posts.data.ts and never come from user
            input, so binding them directly is safe. If posts ever move to a CMS or the
            database this must be sanitised first.
          -->
          <div class="article-body" [innerHTML]="article.body"></div>
        </article>
      </main>
    }
  `
})
export class BlogPostComponent {
  /** Route parameter, bound by the router's component input binding. */
  readonly slug = input<string>('');

  private readonly router = inject(Router);
  private readonly pageMetadata = inject(PageMetadataService);

  protected readonly post = computed(() => {
    const article = findPostBySlug(this.slug());

    if (!article) {
      // An unknown slug is a genuine 404. Redirecting to the index beats rendering an empty
      // page, which would otherwise be indexed as a thin duplicate of every other bad URL.
      void this.router.navigate(['/blog']);
      return undefined;
    }

    this.pageMetadata.apply({
      title: `${article.title} - ScrumPulse`,
      description: article.summary,
      path: `/blog/${article.slug}`,
      publishedOn: article.publishedOn
    });

    return article;
  });
}
