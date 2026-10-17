import { RenderMode, ServerRoute } from '@angular/ssr';
import { BLOG_POSTS } from './marketing/blog/blog-posts.data';

/**
 * Decides which routes are turned into static HTML at build time.
 *
 * Angular renders in the browser by default, so a crawler fetching a page receives an empty
 * shell and a script bundle. Google can execute JavaScript, but it does so on a second pass
 * that is neither guaranteed nor prompt, and an AdSense reviewer looking at the page sees
 * whatever arrives first. Prerendering the public pages means the article text is present in
 * the delivered HTML rather than assembled afterwards.
 *
 * Only the marketing pages are prerendered. The application is per-squad, session-gated and
 * changes constantly, so there is nothing static to generate and nothing a crawler should
 * index.
 */
export const serverRoutes: ServerRoute[] = [
  // ── Public pages, rendered to static HTML at build time ─────────────────
  { path: '', renderMode: RenderMode.Prerender },
  { path: 'about', renderMode: RenderMode.Prerender },
  { path: 'contact', renderMode: RenderMode.Prerender },
  { path: 'blog', renderMode: RenderMode.Prerender },
  { path: 'privacy-policy', renderMode: RenderMode.Prerender },
  { path: 'terms', renderMode: RenderMode.Prerender },

  {
    // One static page per article. The slugs come from the same array the routes and the
    // index read, so publishing a post cannot leave its page unprerendered.
    path: 'blog/:slug',
    renderMode: RenderMode.Prerender,
    getPrerenderParams: async () => BLOG_POSTS.map((post) => ({ slug: post.slug }))
  },

  // ── Application, left to the browser ────────────────────────────────────
  // Session-scoped and data-driven. Prerendering it would produce a snapshot of an empty
  // dashboard and ship it to every visitor before their real data loaded.
  { path: 'app', renderMode: RenderMode.Client },

  // Redirect-only paths. Nothing to prerender; the router resolves them client side.
  { path: 'dashboard', renderMode: RenderMode.Client },
  { path: 'privacy', renderMode: RenderMode.Client },
  { path: 'terms-of-service', renderMode: RenderMode.Client },

  { path: '**', renderMode: RenderMode.Client }
];
