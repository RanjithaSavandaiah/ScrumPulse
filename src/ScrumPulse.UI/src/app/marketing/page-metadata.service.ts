import { DOCUMENT, Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';

/** Metadata for one public page. */
export interface PageMetadata {
  title: string;

  /** Meta description. Kept under ~160 characters so search results do not truncate it. */
  description: string;

  /** Route path, used to build the canonical URL. */
  path: string;

  /** ISO publish date, set for articles so they carry an article:published_time tag. */
  publishedOn?: string;
}

/**
 * Sets the title, description, canonical URL and social tags for the public pages.
 *
 * A single-page application keeps the document title from the previous route unless something
 * changes it, so without this every page would report the title of whichever one was loaded
 * first. Search results and link previews both read these tags, and a site whose pages all
 * share one title and no description is a weak signal to a reviewer assessing whether there
 * is real content here.
 */
@Injectable({ providedIn: 'root' })
export class PageMetadataService {
  /** Production origin, used to build absolute canonical and social URLs. */
  private static readonly SiteOrigin = 'https://scrumpulse.onrender.com';

  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  /**
   * Injected rather than using the global `document`.
   *
   * These pages are prerendered in Node, where no global document exists. The injected token
   * resolves to the server-side DOM during prerendering, so the canonical link is written
   * into the static HTML instead of being skipped — which matters, because the canonical tag
   * is only useful if a crawler sees it in the delivered markup.
   */
  private readonly document = inject(DOCUMENT);

  apply(metadata: PageMetadata): void {
    const canonicalUrl = `${PageMetadataService.SiteOrigin}${metadata.path}`;

    this.title.setTitle(metadata.title);

    this.meta.updateTag({ name: 'description', content: metadata.description });

    // Open Graph and Twitter tags so a shared link renders as a card rather than a bare URL.
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

  /**
   * Points the canonical link at the current route.
   *
   * The element is reused rather than appended to, because adding one per navigation would
   * leave a page declaring several conflicting canonicals, which search engines treat as no
   * canonical at all.
   */
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
