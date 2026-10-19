import { DOCUMENT, Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';

export interface PageMetadata {
  title: string;

  /** Under ~160 characters, or search results truncate it. */
  description: string;

  path: string;

  /** ISO date; only articles carry an article:published_time tag. */
  publishedOn?: string;
}

/** Sets title, description, canonical URL and social tags per route. */
@Injectable({ providedIn: 'root' })
export class PageMetadataService {
  private static readonly SiteOrigin = 'https://scrumpulse.onrender.com';

  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  /** The token, not the global: prerendering runs in Node where no document exists. */
  private readonly document = inject(DOCUMENT);

  apply(metadata: PageMetadata): void {
    const canonicalUrl = `${PageMetadataService.SiteOrigin}${metadata.path}`;

    this.title.setTitle(metadata.title);

    this.meta.updateTag({ name: 'description', content: metadata.description });

    // A shared link renders as a card rather than a bare URL.
    this.meta.updateTag({ property: 'og:title', content: metadata.title });
    this.meta.updateTag({ property: 'og:description', content: metadata.description });
    this.meta.updateTag({ property: 'og:url', content: canonicalUrl });
    this.meta.updateTag({ property: 'og:type', content: metadata.publishedOn ? 'article' : 'website' });
    this.meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.meta.updateTag({ name: 'twitter:title', content: metadata.title });
    this.meta.updateTag({ name: 'twitter:description', content: metadata.description });

    if (metadata.publishedOn) {
      this.meta.updateTag({ property: 'article:published_time', content: metadata.publishedOn });
      this.meta.updateTag({ property: 'article:author', content: 'Ranjitha Savandaiah' });
    } else {
      this.meta.removeTag("property='article:published_time'");
      this.meta.removeTag("property='article:author'");
    }

    this.setCanonicalUrl(canonicalUrl);
  }

  // Reuses the existing element. One appended per navigation would leave the page declaring
  // several conflicting canonicals, which search engines read as none.
  private setCanonicalUrl(url: string): void {
    let canonical = this.document.querySelector<HTMLLinkElement>("link[rel='canonical']");

    if (!canonical) {
      canonical = this.document.createElement('link');
      canonical.setAttribute('rel', 'canonical');
      this.document.head.appendChild(canonical);
    }

    canonical.setAttribute('href', url);
  }
}
