import { Routes } from '@angular/router';

/**
 * Public marketing routes sit at the root; the application itself lives under /app.
 *
 * The root previously served the dashboard, so a first-time visitor — and every crawler —
 * landed on a squad view that showed nothing without a join code. That is a poor introduction
 * and the reason the site read as having no content at all. The tool is unchanged and simply
 * moved; /dashboard redirects so existing links and bookmarks keep working.
 */
export const routes: Routes = [
  // ── Public pages ────────────────────────────────────────────────────────
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./marketing/landing/landing.component').then((m) => m.LandingComponent)
  },
  {
    path: 'about',
    loadComponent: () => import('./marketing/about/about.component').then((m) => m.AboutComponent)
  },
  {
    path: 'contact',
    loadComponent: () => import('./marketing/contact/contact.component').then((m) => m.ContactComponent)
  },
  {
    path: 'blog',
    loadComponent: () => import('./marketing/blog/blog-list.component').then((m) => m.BlogListComponent)
  },
  {
    // The slug binds to the component's `slug` input via withComponentInputBinding(),
    // so the article component needs no route subscription to clean up.
    path: 'blog/:slug',
    loadComponent: () => import('./marketing/blog/blog-post.component').then((m) => m.BlogPostComponent)
  },

  // ── Legal ───────────────────────────────────────────────────────────────
  {
    path: 'privacy-policy',
    loadComponent: () =>
      import('./legal/privacy-policy/privacy-policy.component').then((m) => m.PrivacyPolicyComponent)
  },
  { path: 'privacy', redirectTo: 'privacy-policy', pathMatch: 'full' },
  {
    path: 'terms',
    loadComponent: () => import('./legal/terms/terms.component').then((m) => m.TermsComponent)
  },
  { path: 'terms-of-service', redirectTo: 'terms', pathMatch: 'full' },

  // ── Application ─────────────────────────────────────────────────────────
  {
    path: 'app',
    loadComponent: () => import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent)
  },
  { path: 'dashboard', redirectTo: 'app', pathMatch: 'full' },

  // Unknown paths return to the landing page rather than the application, so a mistyped URL
  // does not present a stranger with a squad dashboard.
  { path: '**', redirectTo: '' }
];
